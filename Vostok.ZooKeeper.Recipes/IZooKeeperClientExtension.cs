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

namespace Vostok.ZooKeeper.Recipes
{
    [PublicAPI]
    public static class IZooKeeperClientExtension
    {
        /// <summary>
        /// <para>Creates new node specified in given <paramref name="request" />.</para>
        /// <para>The name of this node will be suffixed with GUID.</para>
        /// <para>If node creation fails with network error, the normal retry mechanism will occur.</para>
        /// <para>On the retry, the parent path is first searched for a node that has the GUID in it.</para>
        /// <para>If that node is found, it is assumed to be the lost node that was successfully created.</para>
        /// <para>This node will be deleted, before new create attempt.</para>
        /// <para>Check returned <see cref="CreateResult"/> to see if operation was successful.</para>
        /// </summary>
        public static async Task<CreateResult> CreateProtectedAsync([NotNull] this IZooKeeperClient client, [NotNull] CreateRequest request, [NotNull] ILog log)
        {
            var protectedPath = request.CreateMode.IsSequential() ? $"{request.Path}-{Guid.NewGuid():N}-" : $"{request.Path}-{Guid.NewGuid():N}";
            request = request.WithPath(protectedPath);

            log.Info("Creating a protected node with request '{Request}'..", request);

            while (true)
            {
                var result = await client.CreateAsync(request).ConfigureAwait(false);

                if (!result.Status.IsNetworkError())
                    return result;

                var deleteRequest = new DeleteRequest(request.Path);
                var delete = await client.DeleteProtectedAsync(deleteRequest, log).ConfigureAwait(false);
                if (!delete.IsSuccessful)
                    return CreateResult.Unsuccessful(delete.Status, delete.Path, delete.Exception);
            }
        }

        /// <summary>
        /// <para>Deletes the node specified in given <paramref name="request"/>.</para>
        /// <para>The parent path is searched for a node that is prefixed with given path.</para>
        /// <para>Check returned <see cref="DeleteResult"/> to see if operation was successful.</para>
        /// </summary>
        public static async Task<DeleteResult> DeleteProtectedAsync([NotNull] this IZooKeeperClient client, [NotNull] DeleteRequest request, [NotNull] ILog log)
        {
            log.Info("Deleting a protected node with request '{Request}'..", request);

            var path = request.Path;
            var parent = ZooKeeperPath.GetParentPath(path);
            if (parent == null)
                return DeleteResult.Unsuccessful(ZooKeeperStatus.BadArguments, request.Path, new Exception($"Node with path '{path}' has no parent."));
            var name = ZooKeeperPath.GetNodeName(path);
            if (name == null)
                return DeleteResult.Unsuccessful(ZooKeeperStatus.BadArguments, request.Path, new Exception($"Node with path '{path}' has no name."));

            while (true)
            {
                var children = await client.GetChildrenAsync(parent).ConfigureAwait(false);
                if (children.Status.IsNetworkError())
                    continue;
                if (!children.IsSuccessful)
                    return DeleteResult.Unsuccessful(children.Status, children.Path, children.Exception);

                var found = children.ChildrenNames.FirstOrDefault(c => c.StartsWith(name));
                if (found == null)
                    return DeleteResult.Unsuccessful(ZooKeeperStatus.NodeNotFound, path, null);

                var delete = await client.DeleteAsync(ZooKeeperPath.Combine(parent, found)).ConfigureAwait(false);
                if (delete.Status.IsNetworkError())
                    continue;
                return delete;
            }
        }

        /// <summary>
        /// Waits until a node with given <param name="path"> will be have the smallest sequential index.</param>
        /// </summary>
        public static async Task<bool> WaitForLeadershipAsync([NotNull] this IZooKeeperClient client, [NotNull] string path, [NotNull] ILog log, CancellationToken cancellationToken = default)
        {
            log.Info("Waiting while a node with path '{Path}' becomes a leader..", path);

            var parent = ZooKeeperPath.GetParentPath(path) ?? throw new Exception($"Node with path '{path}' has no parent.");
            var index = ZooKeeperPath.GetSequentialNodeIndex(path) ?? throw new Exception($"Node with path '{path}' has no index.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var exists = await client.ExistsAsync(path).ConfigureAwait(false);
                if (exists.Status.IsNetworkError())
                    continue;
                if (!exists.IsSuccessful || !exists.Exists)
                    return false;

                var children = await client.GetChildrenAsync(parent).ConfigureAwait(false);
                if (children.Status.IsNetworkError())
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

                await client.WaitForDisappearanceAsync(new[] {path, previous}, log, cancellationToken).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// <para>Waits until one of the given nodes will be disappear, or client connection will be lost.</para>
        /// </summary>
        public static async Task WaitForDisappearanceAsync([NotNull] this IZooKeeperClient client, string[] paths, [NotNull] ILog log, CancellationToken cancellationToken = default)
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