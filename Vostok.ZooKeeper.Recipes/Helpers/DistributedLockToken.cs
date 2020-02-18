using System;
using System.Threading.Tasks;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Context;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Recipes.Helpers
{
    /// <inheritdoc/>
    internal class DistributedLockToken : IDistributedLockToken
    {
        private readonly IZooKeeperClient client;
        private readonly string path;
        private readonly OperationContextToken logToken;
        private readonly ILog log;
        private readonly AtomicBoolean disposed = false;
        private readonly TaskCompletionSource<DeleteResult> deleteResult = new TaskCompletionSource<DeleteResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        internal DistributedLockToken(IZooKeeperClient client, string path, OperationContextToken logToken, ILog log)
        {
            this.client = client;
            this.path = path;
            this.logToken = logToken;
            this.log = log;

            Task.Run(
                async () =>
                {
                    await ZooKeeperNodeHelper
                        .WaitForDisappearanceAsync(client, new[] {path}, log)
                        .ContinueWith(
                            _ =>
                            {
                                if (!disposed)
                                {
                                    log.Info("Lost a lock with path '{Path}'.", path);
                                    Dispose();
                                }
                            })
                        .ConfigureAwait(false);
                });
        }

        /// <inheritdoc/>
        public bool IsAcquired => !disposed;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (disposed.TrySetTrue())
            {
                log.Info("Releasing a lock with path '{Path}'.", path);

                var delete = client.DeleteProtectedAsync(new DeleteRequest(path), log).GetAwaiter().GetResult();
                deleteResult.TrySetResult(delete);
                if (!delete.IsSuccessful)
                    throw new Exception("Failed to delete lock node.", delete.Exception);

                log.Info("Lock with path '{Path}' successfully released.", path);

                // ReSharper disable once PossiblyImpureMethodCallOnReadonlyVariable
                logToken.Dispose();
            }
            else
            {
                var delete = deleteResult.Task.GetAwaiter().GetResult();
                if (!delete.IsSuccessful)
                    throw new Exception("Failed to delete lock node.", delete.Exception);
            }
        }
    }
}