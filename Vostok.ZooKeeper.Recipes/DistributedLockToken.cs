using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Threading;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Recipes.Helpers;

namespace Vostok.ZooKeeper.Recipes
{
    [PublicAPI]
    public class DistributedLockToken : IDisposable
    {
        private readonly IZooKeeperClient client;
        private readonly string path;
        private readonly AtomicBoolean disposed = false;

        internal DistributedLockToken(IZooKeeperClient client, string path)
        {
            this.client = client;
            this.path = path;

            //Task.Run(async () => await client.WaitForDisappearsAsync(path)).ContinueWith(_ => Dispose());
        }

        public bool IsAlive => !disposed;

        public void Dispose()
        {
            //if (!disposed.TrySetTrue())
            //    return;

            //if (!client.DeleteProtectedAsync(path).GetAwaiter().GetResult())
            //    throw new Exception("Failed to delete lock node.");
        }
    }
}