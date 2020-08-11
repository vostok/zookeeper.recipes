using System;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ZooKeeper.Recipes.Helpers
{
    internal static class ZooKeeperPathHelper
    {
        public static string BuildProtectedNodePath(CreateRequest request, Guid? id)
        {
            id = id ?? Guid.NewGuid();

            var parent = ZooKeeperPath.GetParentPath(request.Path) ?? throw new Exception($"Path '{request.Path}' has no parent.");
            var name = ZooKeeperPath.GetNodeName(request.Path) ?? throw new Exception($"Path '{request.Path}' has no name.");

            var path = ZooKeeperPath.Combine(parent, $"_c_{id:D}-{name}");

            return path;
        }
    }
}