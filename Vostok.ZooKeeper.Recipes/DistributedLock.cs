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
    /// <inheritdoc/>
    [PublicAPI]
    public class DistributedLock : IDistributedLock
    {
        private readonly IZooKeeperClient client;
        private readonly ILog log;
        private readonly DistributedLockSettings settings;
        private readonly string lockFolder;
        private readonly string lockPath;
        private readonly byte[] lockData;

        public DistributedLock([NotNull] IZooKeeperClient client, [NotNull] DistributedLockSettings settings, [CanBeNull] ILog log = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.log = (log ?? LogProvider.Get()).ForContext<DistributedLock>().WithOperationContext();
            this.client = client ?? throw new ArgumentNullException(nameof(client));

            lockFolder = settings.Path;
            lockPath = ZooKeeperPath.Combine(lockFolder, "lock");
            lockData = NodeDataHelper.GetNodeData();
        }

        /// <inheritdoc/>
        public async Task<IDistributedLockToken> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            using (var timeoutCancellation = new CancellationTokenSource(timeout))
            using (var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCancellation.Token))
            {
                var linkedCancellationToken = linkedCancellation.Token;

                while (!linkedCancellationToken.IsCancellationRequested)
                {
                    var @lock = await AcquireOnceAsync(linkedCancellationToken).ConfigureAwait(false);

                    if (@lock != null)
                        return @lock;
                }
            }

            if (cancellationToken.IsCancellationRequested)
                throw new OperationCanceledException($"Lock '{lockFolder}' acqure has been canceled.");
            return null;
        }

        /// <inheritdoc/>
        public async Task<IDistributedLockToken> AcquireAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var @lock = await AcquireOnceAsync(cancellationToken).ConfigureAwait(false);

                if (@lock != null)
                    return @lock;
            }

            throw new OperationCanceledException($"Lock '{lockFolder}' acqure has been canceled.");
        }

        private async Task<IDistributedLockToken> AcquireOnceAsync(CancellationToken cancellationToken)
        {
            log.Info("Acquiring lock..");

            var create = await client.CreateProtectedAsync(
                    new CreateRequest(lockPath, CreateMode.EphemeralSequential)
                    {
                        Data = lockData
                    },
                    log)
                .ConfigureAwait(false);

            if (!create.IsSuccessful)
                throw new Exception("Failed to create lock node.", create.Exception);

            if (await ZooKeeperNodeHelper.WaitForLeadershipAsync(client, create.NewPath, log, cancellationToken).ConfigureAwait(false))
            {
                log.Info("Lock with path '{Path}' was successfully acquired.", create.NewPath);

                return new DistributedLockToken(client, create.NewPath, log);
            }

            log.Info("Lock with path '{Path}' was not acquired.", create.NewPath);
            var delete = await client.DeleteProtectedAsync(new DeleteRequest(create.NewPath), log).ConfigureAwait(false);

            if (!delete.IsSuccessful)
                throw new Exception("Failed to delete lock node.", delete.Exception);

            return null;
        }
    }
}