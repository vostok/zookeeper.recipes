using System;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Recipes.Helpers
{
    internal static class IZooKeeperClientExtensions
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
        public static async Task<CreateResult> CreateProtectedAsync([NotNull] this IZooKeeperClient client, [NotNull] CreateRequest request, [NotNull] ILog log, Guid? lockId = null)
        {
            lockId = lockId ?? Guid.NewGuid();
            var protectedPath = request.CreateMode.IsSequential() ? $"{request.Path}-{lockId:N}-" : $"{request.Path}-{lockId:N}";
            request = request.WithPath(protectedPath);

            log.Info("Creating a protected node with path '{Path}'..", protectedPath);

            while (true)
            {
                var result = await client.CreateAsync(request).ConfigureAwait(false);

                if (!result.IsRetriableError())
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
            log.Info("Deleting a protected node with path '{Path}'..", request.Path);

            var path = request.Path;
            
            var parent = ZooKeeperPath.GetParentPath(path);
            if (parent == null)
                return DeleteResult.Unsuccessful(ZooKeeperStatus.BadArguments, request.Path, new Exception($"Node with path '{path}' has no parent."));
            
            var name = ZooKeeperPath.GetNodeName(path);
            if (name == null)
                return DeleteResult.Unsuccessful(ZooKeeperStatus.BadArguments, request.Path, new Exception($"Node with path '{path}' has no name."));

            while (true)
            {
                var childrenResult = await client.GetChildrenAsync(parent).ConfigureAwait(false);
                if (childrenResult.IsRetriableError())
                    continue;
                
                if (!childrenResult.IsSuccessful)
                    return DeleteResult.Unsuccessful(childrenResult.Status, childrenResult.Path, childrenResult.Exception);

                // CR(iloktionov): What if there's more than one due to false negatives?
                var found = childrenResult.ChildrenNames.FirstOrDefault(c => c.StartsWith(name));
                if (found == null)
                    return DeleteResult.Unsuccessful(ZooKeeperStatus.NodeNotFound, path, null);

                var deleteResult = await client.DeleteAsync(ZooKeeperPath.Combine(parent, found)).ConfigureAwait(false);
                if (deleteResult.IsRetriableError())
                    continue;

                return deleteResult;
            }
        }
    }
}