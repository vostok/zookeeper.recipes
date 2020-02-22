using JetBrains.Annotations;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ZooKeeper.Recipes.Helpers
{
    internal static class ZooKeeperResultExtensions
    {
        // CR(iloktionov): Is NotReadonlyOperation status really retriable?
        public static bool IsRetriableError([NotNull] this ZooKeeperResult result) =>
            result.Status == ZooKeeperStatus.UnknownError ||
            result.Status == ZooKeeperStatus.NotConnected ||
            result.Status == ZooKeeperStatus.ConnectionLoss ||
            result.Status == ZooKeeperStatus.SessionExpired ||
            result.Status == ZooKeeperStatus.SessionMoved ||
            result.Status == ZooKeeperStatus.Timeout ||
            result.Status == ZooKeeperStatus.NotReadonlyOperation;
    }
}