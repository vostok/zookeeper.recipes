using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Extensions;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Recipes.Helpers
{
    internal static class ZooKeeperNodeHelper
    {
        /// <summary>
        /// Waits until a node with given <param name="path"> will have the smallest sequential index.</param>
        /// </summary>
        public static async Task<bool> WaitForLeadershipAsync([NotNull] IZooKeeperClient client, [NotNull] string path, [NotNull] ILog log, CancellationToken cancellationToken = default)
        {
            log.Info("Waiting for the node with path '{Path}' to become the lock holder..", path);

            var parent = ZooKeeperPath.GetParentPath(path) ?? throw new Exception($"Node with path '{path}' has no parent.");
            var index = ZooKeeperPath.GetSequentialNodeIndex(path) ?? throw new Exception($"Node with path '{path}' has no index.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var existsResult = await client.ExistsAsync(path).ConfigureAwait(false);
                if (existsResult.IsRetriableNetworkError())
                {
                    await Task.Delay(100).ConfigureAwait(false);
                    continue;
                }

                if (!existsResult.IsSuccessful || !existsResult.Exists || cancellationToken.IsCancellationRequested)
                    return false;

                var childrenResult = await client.GetChildrenAsync(parent).ConfigureAwait(false);
                if (childrenResult.IsRetriableNetworkError())
                {
                    await Task.Delay(100).ConfigureAwait(false);
                    continue;
                }

                if (!childrenResult.IsSuccessful || cancellationToken.IsCancellationRequested)
                    return false;

                var (previousName, _) = childrenResult.ChildrenNames
                    .Select(name => (name, index: ZooKeeperPath.GetSequentialNodeIndex(name)))
                    .Where(n => n.index.HasValue)
                    .Select(n => (n.name, index: n.index.Value))
                    .OrderByDescending(n => n.index)
                    .FirstOrDefault(n => n.index < index);

                if (previousName == null)
                    return true;

                var previous = ZooKeeperPath.Combine(parent, previousName);

                await WaitForDisappearanceAsync(client, new[] {path, previous}, log, cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// <para>Waits until one of the given nodes will disappear, or client connection will be lost.</para>
        /// </summary>
        public static async Task WaitForDisappearanceAsync([NotNull] IZooKeeperClient client, string[] paths, [NotNull] ILog log, CancellationToken cancellationToken = default)
        {
            var wait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var observer = new AdHocConnectionStateObserver(
                s =>
                {
                    if (s != ConnectionState.Connected)
                        wait.TrySetResult(true);
                },
                () => wait.TrySetResult(true));
            var watcher = new AdHocNodeWatcher(
                (changedType, __) =>
                {
                    if (changedType == NodeChangedEventType.Deleted)
                        wait.TrySetResult(true);
                });

            using (cancellationToken.Register(o => ((TaskCompletionSource<bool>)o).TrySetCanceled(), wait))
            using (client.OnConnectionStateChanged.Subscribe(observer))
            {
                foreach (var path in paths)
                {
                    var existsResult = await client.ExistsAsync(new ExistsRequest(path) {Watcher = watcher, IgnoreWatchersCache = true}).ConfigureAwait(false);
                    if (!existsResult.IsSuccessful || !existsResult.Exists || cancellationToken.IsCancellationRequested)
                        return;
                }

                log.Info("Waiting until one of the nodes with following paths disappears: {Paths}.", paths);

                await wait.Task.SilentlyContinue().ConfigureAwait(false);
            }
        }
    }
}