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

namespace Vostok.ZooKeeper.Recipes.Helpers
{
    internal static class ZooKeeperNodeHelper
    {
        /// <summary>
        /// Waits until a node with given <param name="path"> will have the smallest sequential index.</param>
        /// </summary>
        public static async Task<bool> WaitForLeadershipAsync([NotNull] IZooKeeperClient client, [NotNull] string path, [NotNull] ILog log, CancellationToken cancellationToken = default)
        {
            log.Info("Waiting while a node with path '{Path}' becomes a leader..", path);

            var parent = ZooKeeperPath.GetParentPath(path) ?? throw new Exception($"Node with path '{path}' has no parent.");
            var index = ZooKeeperPath.GetSequentialNodeIndex(path) ?? throw new Exception($"Node with path '{path}' has no index.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var exists = await client.ExistsAsync(path).ConfigureAwait(false);
                if (exists.IsRetryableError())
                    continue;
                if (!exists.IsSuccessful || !exists.Exists)
                    return false;

                var children = await client.GetChildrenAsync(parent).ConfigureAwait(false);
                if (children.IsRetryableError())
                    continue;
                if (!children.IsSuccessful)
                    return false;

                var (previousName, _) = children.ChildrenNames
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
                    var exists = await client.ExistsAsync(new ExistsRequest(path) {Watcher = watcher, IgnoreWatchersCache = true}).ConfigureAwait(false);
                    if (!exists.IsSuccessful || !exists.Exists)
                        return;
                }

                log.Info("Waiting until a nodes with paths '{Path}' disappear..", string.Join(", ", paths));
                await wait.Task.SilentlyContinue().ConfigureAwait(false);
            }
        }
    }
}