using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Context;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Recipes.Helpers;

namespace Vostok.ZooKeeper.Recipes
{
    // CR(iloktionov): 1. A general question: has this lock implementation been tested for compatibility with legacy node format?
    // CR(iloktionov): 2. I think we should verbally discuss the merits of using operation context for logging.

    /// <inheritdoc/>
    [PublicAPI]
    public class DistributedLock : IDistributedLock
    {
        private readonly IZooKeeperClient client;
        private readonly ILog log;
        private readonly string lockFolder;
        private readonly string lockPath;
        private readonly byte[] lockData;

        public DistributedLock([NotNull] IZooKeeperClient client, [NotNull] DistributedLockSettings settings, [CanBeNull] ILog log = null)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            // CR(iloktionov): We should also probably let the user decide whether they want to see operation context or not.
            this.log = (log ?? LogProvider.Get()).ForContext<DistributedLock>();
            this.client = client ?? throw new ArgumentNullException(nameof(client));

            lockFolder = settings.Path;
            lockPath = ZooKeeperPath.Combine(lockFolder, "lock");
            lockData = NodeDataHelper.GetNodeData();
        }

        /// <summary>
        /// <inheritdoc/>
        /// <para>Execution time of this method may significantly exceed given <paramref name="timeout"/> in case of ZooKeeper unavailability.</para>
        /// </summary>
        public async Task<IDistributedLockToken> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            using (var timeoutCancellation = new CancellationTokenSource(timeout))
            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token))
            {
                var linkedCancellationToken = linkedCancellation.Token;
                var lockId = Guid.NewGuid();

                while (!linkedCancellationToken.IsCancellationRequested)
                {
                    var token = await AcquireOnceAsync(linkedCancellationToken, lockId).ConfigureAwait(false);
                    if (token != null)
                        return token;
                }
            }

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException($"Lock '{lockFolder}' acquisition has been canceled.");

            return null;
        }

        /// <inheritdoc/>
        public async Task<IDistributedLockToken> AcquireAsync(CancellationToken cancellationToken = default)
        {
            var tokenId = Guid.NewGuid();

            while (!cancellationToken.IsCancellationRequested)
            {
                var token = await AcquireOnceAsync(cancellationToken, tokenId).ConfigureAwait(false);
                if (token != null)
                    return token;
            }

            throw new OperationCanceledException($"Lock '{lockFolder}' acquisition has been canceled.");
        }

        private async Task<IDistributedLockToken> AcquireOnceAsync(CancellationToken cancellationToken, Guid tokenId)
        {
            var logToken = tokenId.CreateOperationContextToken();

            try
            {
                log.Info("Acquiring lock..");

                var createResult = await client.CreateProtectedAsync(
                        new CreateRequest(lockPath, CreateMode.EphemeralSequential)
                        {
                            Data = lockData
                        },
                        log,
                        tokenId)
                    .ConfigureAwait(false);
                createResult.EnsureSuccess();

                if (await ZooKeeperNodeHelper.WaitForLeadershipAsync(client, createResult.NewPath, log, cancellationToken).ConfigureAwait(false))
                {
                    log.Info("Lock token with path '{Path}' was successfully acquired.", createResult.NewPath);

                    return new DistributedLockToken(client, tokenId, createResult.NewPath, logToken, log);
                }

                var deleteResult = await client.DeleteProtectedAsync(new DeleteRequest(createResult.NewPath), log).ConfigureAwait(false);
                deleteResult.EnsureSuccess();

                return null;
            }
            catch
            {
                logToken.Dispose();
                throw;
            }
        }
    }
}