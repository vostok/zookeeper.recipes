using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Recipes.Helpers;

namespace Vostok.ZooKeeper.Recipes
{
    [PublicAPI]
    public class DistributedLock
    {
        private readonly ExtendedZooKeeperClient client;
        private readonly ILog log;
        private readonly DistributedLockSettings settings;
        private readonly string lockFolder;
        private readonly string lockPath;
        private readonly byte[] lockData;

        public DistributedLock([NotNull] IZooKeeperClient client, [NotNull] DistributedLockSettings settings, [CanBeNull] ILog log = null)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.log = log = (log ?? LogProvider.Get()).ForContext<DistributedLock>().ForContext(settings.Path);
            this.client = new ExtendedZooKeeperClient(client ?? throw new ArgumentNullException(nameof(client)), log);
            
            lockFolder = settings.Path;
            lockPath = ZooKeeperPath.Combine(lockFolder, "lock");
            lockData = NodeDataHelper.GetNodeData();
        }

        public async Task<DistributedLockToken> AcquireAsync(CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var @lock = await AcquireOnceAsync(cancellationToken).ConfigureAwait(false);

                if (@lock != null)
                    return @lock;
            }

            throw new OperationCanceledException($"Distributed lock '{lockFolder}' acqure has been canceled.");
        }

        private async Task<DistributedLockToken> AcquireOnceAsync(CancellationToken cancellationToken)
        {
            log.Info("Acquiring lock..");

            var lockNode = await client.CreateProtectedAsync(lockPath, lockData);

            if (!lockNode.IsSuccessful)
                throw new Exception("Failed to create lock node.", lockNode.Exception);

            if (await client.WaitForLeadershipAsync(lockNode.NewPath, cancellationToken).ConfigureAwait(false))
            {
                log.Info("Lock with path '{Path}' was acquired.", lockNode.NewPath);

                return new DistributedLockToken(client, lockNode.NewPath);
            }

            log.Info("Lock with path '{Path}' was not acquired.", lockNode.NewPath);
            if (!await client.DeleteProtectedAsync(lockNode.NewPath).ConfigureAwait(false))
                throw new Exception("Failed to delete lock node.");

            return null;
        }
    }
}