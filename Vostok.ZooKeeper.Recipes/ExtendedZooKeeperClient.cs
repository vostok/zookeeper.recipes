using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Recipes
{
    /// <inheritdoc/>
    /// <para>Client's additional operations acting on these nodes are:</para>
    /// <list type="bullet">
    ///     <item><description><see cref="CreateProtectedAsync"/></description></item>
    ///     <item><description><see cref="DeleteProtectedAsync"/></description></item>
    ///     <item><description><see cref="WaitForLeadershipAsync"/></description></item>
    ///     <item><description><see cref="WaitForDisappearsAsync"/></description></item>
    /// </list>
    [PublicAPI]
    public class ExtendedZooKeeperClient : IZooKeeperClient
    {
        private readonly IZooKeeperClient client;
        private readonly ILog log;

        public ExtendedZooKeeperClient(IZooKeeperClient client, ILog log)
        {
            this.client = client;
            this.log = log;
        }

        public async Task<CreateResult> CreateProtectedAsync(string path, byte[] data)
        {
            path = $"{path}-{Guid.NewGuid():N}-";
            log.Info("Creating a protected node with path '{Path}'..", path);

            while (true)
            {
                var result = await client.CreateAsync(path, CreateMode.EphemeralSequential, data);

                if (!result.Status.IsNetworkError())
                    return result;

                if (!await DeleteProtectedAsync(path).ConfigureAwait(false))
                    return CreateResult.Unsuccessful(ZooKeeperStatus.UnknownError, path, new Exception("Failed to remove created node."));
            }
        }

        public async Task<bool> DeleteProtectedAsync(string path)
        {
            log.Info("Deleting a protected node with path '{Path}'..", path);

            var parent = ZooKeeperPath.GetParentPath(path) ?? throw new Exception($"Node with path '{path}' has no parent.");
            var name = ZooKeeperPath.GetNodeName(path) ?? throw new Exception($"Node with path '{path}' has no name.");

            while (true)
            {
                var children = await client.GetChildrenAsync(parent);
                if (children.Status.IsNetworkError())
                    continue;
                if (!children.IsSuccessful)
                    return children.Status == ZooKeeperStatus.NodeNotFound;

                var found = children.ChildrenNames.FirstOrDefault(c => c.Contains(name));

                if (found == null)
                    return true;

                var delete = await client.DeleteAsync(ZooKeeperPath.Combine(parent, found));

                if (delete.Status.IsNetworkError())
                    continue;
                return delete.IsSuccessful;
            }
        }

        public async Task<bool> WaitForLeadershipAsync(string path, CancellationToken cancellationToken)
        {
            log.Info("Waiting while a node with path '{Path}' becomes a leader..", path);

            var parent = ZooKeeperPath.GetParentPath(path) ?? throw new Exception($"Node with path '{path}' has no parent.");
            var index = ZooKeeperPath.GetSequentialNodeIndex(path) ?? throw new Exception($"Node with path '{path}' has no index.");

            while (!cancellationToken.IsCancellationRequested)
            {
                var exists = await client.ExistsAsync(path);
                if (exists.Status.IsNetworkError())
                    continue;
                if (!exists.IsSuccessful || !exists.Exists)
                    return false;

                var children = await client.GetChildrenAsync(parent);
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

                await WaitForDisappearsAsync(path, previous);
            }

            return false;
        }

        public async Task WaitForDisappearsAsync(params string[] paths)
        {
            var wait = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var observer = new AdHocConnectionStateObserver(_ => wait.TrySetResult(true), () => wait.TrySetResult(true));
            var watcher = new AdHocNodeWatcher((_, __) => wait.TrySetResult(true));

            using (client.OnConnectionStateChanged.Subscribe(observer))
            {
                foreach (var path in paths)
                {
                    var exists = await client.ExistsAsync(new ExistsRequest(path) { Watcher = watcher, IgnoreWatchersCache = true });
                    if (!exists.IsSuccessful)
                        return;
                }

                log.Info("Waiting to a nodes with paths '{Path}' changes..", string.Join(", ", paths));
                await wait.Task.ConfigureAwait(false);
            }
        }

        #region InheritedClientOperations

        public Task<CreateResult> CreateAsync(CreateRequest request) =>
            client.CreateAsync(request);

        public Task<DeleteResult> DeleteAsync(DeleteRequest request) =>
            client.DeleteAsync(request);

        public Task<SetDataResult> SetDataAsync(SetDataRequest request) =>
            client.SetDataAsync(request);

        public Task<ExistsResult> ExistsAsync(ExistsRequest request) =>
            client.ExistsAsync(request);

        public Task<GetChildrenResult> GetChildrenAsync(GetChildrenRequest request) =>
            client.GetChildrenAsync(request);

        public Task<GetDataResult> GetDataAsync(GetDataRequest request) =>
            client.GetDataAsync(request);

        public IObservable<ConnectionState> OnConnectionStateChanged =>
            client.OnConnectionStateChanged;
        public ConnectionState ConnectionState =>
            client.ConnectionState;
        public long SessionId =>
            client.SessionId;

        #endregion
    }
}