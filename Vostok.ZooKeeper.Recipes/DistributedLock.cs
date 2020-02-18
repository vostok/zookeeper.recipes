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
    /// <summary>
    /// <para><see cref="DistributedLock"/> is an entry point for acquiring distributed lock.</para>
    /// <para>See <see cref="AcquireAsync"/> and <see cref="DistributedLockToken"/> for details.</para>
    /// </summary>
    [PublicAPI]
    public class DistributedLock
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

        /// <summary>
        /// <para>Acquires distributed lock.</para>
        /// <para>Returns <see cref="DistributedLockToken"/> that should be disposed after use.</para>
        /// </summary>
        public async Task<DistributedLockToken> AcquireAsync(CancellationToken cancellationToken = default)
        {
            using (new OperationContextToken(lockFolder))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var @lock = await AcquireOnceAsync(cancellationToken).ConfigureAwait(false);

                    if (@lock != null)
                        return @lock;
                }

                throw new OperationCanceledException($"Lock '{lockFolder}' acqure has been canceled.");
            }
        }

        private async Task<DistributedLockToken> AcquireOnceAsync(CancellationToken cancellationToken)
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

            if (await client.WaitForLeadershipAsync(create.NewPath, log, cancellationToken).ConfigureAwait(false))
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