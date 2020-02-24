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

            // CR(iloktionov): What's the purpose of this data? To be human-readable for debug purposes or to be machine-readable?
            // If it's the first one, then why is it serialized in binary format?
            // If it's the last one, then what consumers for it do we know of?
            lockData = NodeDataHelper.GetNodeData();
        }

        // CR(iloktionov): Document the imprecise nature of timeout in this method (it can be significantly exceeded in case of ZK unavailability).
        /// <inheritdoc/>
        public async Task<IDistributedLockToken> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            using (var timeoutCancellation = new CancellationTokenSource(timeout))
            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token))
            {
                var linkedCancellationToken = linkedCancellation.Token;

                while (!linkedCancellationToken.IsCancellationRequested)
                {
                    var token = await AcquireOnceAsync(linkedCancellationToken).ConfigureAwait(false);
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
            while (!cancellationToken.IsCancellationRequested)
            {
                var token = await AcquireOnceAsync(cancellationToken).ConfigureAwait(false);
                if (token != null)
                    return token;
            }

            throw new OperationCanceledException($"Lock '{lockFolder}' acquisition has been canceled.");
        }

        private async Task<IDistributedLockToken> AcquireOnceAsync(CancellationToken cancellationToken)
        {
            // CR(iloktionov): Is this really the "lock" id, considering that we may cycle through several of these during a single acquire attempt?
            var lockId = Guid.NewGuid();
            var logTokenValue = $"Lock-{lockId.ToString("N").Substring(0, 8)}";
            var logToken = new OperationContextToken(logTokenValue);

            try
            {
                log.Info("Acquiring lock..");

                var createResult = await client.CreateProtectedAsync(
                        new CreateRequest(lockPath, CreateMode.EphemeralSequential)
                        {
                            Data = lockData
                        },
                        log,
                        lockId)
                    .ConfigureAwait(false);

                // CR(iloktionov): Does this convey more information than simple createResult.EnsureSuccess()?
                if (!createResult.IsSuccessful)
                    throw new Exception("Failed to create lock node.", createResult.Exception);

                if (await ZooKeeperNodeHelper.WaitForLeadershipAsync(client, createResult.NewPath, log, cancellationToken).ConfigureAwait(false))
                {
                    // CR(iloktionov): Is this really the lock path? Isn't it just the path to our not-so-meaningful participant node?
                    log.Info("Lock with path '{Path}' was successfully acquired.", createResult.NewPath);

                    return new DistributedLockToken(client, createResult.NewPath, logToken, logTokenValue, log);
                }

                // CR(iloktionov): What's the purpose of this log message?
                log.Info("Lock with path '{Path}' was not acquired.", createResult.NewPath);

                // CR(iloktionov): Does this convey more information than simple deleteResult.EnsureSuccess()?
                var deleteResult = await client.DeleteProtectedAsync(new DeleteRequest(createResult.NewPath), log).ConfigureAwait(false);
                if (!deleteResult.IsSuccessful)
                    throw new Exception("Failed to delete lock node.", deleteResult.Exception);

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