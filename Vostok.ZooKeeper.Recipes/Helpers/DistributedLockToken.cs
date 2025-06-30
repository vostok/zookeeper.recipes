using System.Threading;
using System.Threading.Tasks;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
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
        private readonly ILog log;
        private readonly AtomicBoolean disposed = false;
        private readonly TaskCompletionSource<DeleteResult> deleteResult = new TaskCompletionSource<DeleteResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        internal DistributedLockToken(IZooKeeperClient client, string path, ILog log)
        {
            this.client = client;
            this.path = path;
            this.log = log;

            Task.Run(async () =>
            {
                await ZooKeeperNodeHelper
                    .WaitForDisappearanceAsync(client, new[] {path}, log)
                    .ContinueWith(_ =>
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
        public CancellationToken CancellationToken => cancellationTokenSource.Token;

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
#if NET
        public async ValueTask DisposeAsync()
#else
        private async Task DisposeAsync()
#endif
        {
            if (disposed.TrySetTrue())
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();

                log.Info("Releasing a lock with path '{Path}'.", path);

                var delete = await client.DeleteProtectedAsync(new DeleteRequest(path), log);
                deleteResult.TrySetResult(delete);
                delete.EnsureSuccess();

                log.Info("Lock with path '{Path}' successfully released.", path);
            }
            else
            {
                var delete = await deleteResult.Task;
                delete.EnsureSuccess();
            }
        }
    }
}