using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Context;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ZooKeeper.Recipes
{
    [PublicAPI]
    public class DistributedLockToken : IDisposable
    {
        private readonly IZooKeeperClient client;
        private readonly string path;
        private readonly ILog log;
        private readonly string logContextToken;
        private readonly AtomicBoolean disposed = false;

        internal DistributedLockToken(IZooKeeperClient client, string path, ILog log)
        {
            this.client = client;
            this.path = path;
            this.log = log;
            logContextToken = ZooKeeperPath.GetParentPath(path) ?? "";

            Task.Run(
                async () =>
                {
                    await client
                        .WaitForDisappearanceAsync(new[] {path}, log)
                        .ContinueWith(_ => Dispose());
                });
        }

        public bool IsAcquired => !disposed;

        public void Dispose()
        {
            using (new OperationContextToken(logContextToken))
            {
                if (!disposed.TrySetTrue())
                    return;

                log.Info("Releasing a lock with path '{Path}'.", path);

                var delete = client.DeleteProtectedAsync(new DeleteRequest(path), log).GetAwaiter().GetResult();
                if (!delete.IsSuccessful)
                    throw new Exception("Failed to delete lock node.", delete.Exception);

                log.Info("Lock with path '{Path}' successfully released.", path);
            }
        }
    }
}