using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Context;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Recipes
{
    /// <summary>
    /// <para>Represents a distributed lock token.</para>
    /// <para>Should be periodically checked, whether or not this lock token is still alive, by calling <see cref="IsAcquired"/>.</para>
    /// <para>Call <see cref="Dispose"/> to release lock.</para>
    /// </summary>
    [PublicAPI]
    public class DistributedLockToken : IDisposable
    {
        private readonly IZooKeeperClient client;
        private readonly string path;
        private readonly ILog log;
        private readonly string logContextToken;
        private readonly AtomicBoolean disposed = false;
        private readonly TaskCompletionSource<DeleteResult> deleteResult = new TaskCompletionSource<DeleteResult>(TaskCreationOptions.RunContinuationsAsynchronously);

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
                        .ContinueWith(
                            _ =>
                            {
                                log.Info("Lost a lock with path '{Path}'.", path);
                                Dispose();
                            })
                        .ConfigureAwait(false);
                });
        }

        public bool IsAcquired => !disposed;

        public void Dispose()
        {
            using (new OperationContextToken(logContextToken))
            {
                if (disposed.TrySetTrue())
                {
                    log.Info("Releasing a lock with path '{Path}'.", path);

                    var delete = client.DeleteProtectedAsync(new DeleteRequest(path), log).GetAwaiter().GetResult();
                    deleteResult.TrySetResult(delete);
                    if (!delete.IsSuccessful)
                        throw new Exception("Failed to delete lock node.", delete.Exception);

                    log.Info("Lock with path '{Path}' successfully released.", path);
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
}