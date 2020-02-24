using System;
using JetBrains.Annotations;
using Vostok.Logging.Context;

namespace Vostok.ZooKeeper.Recipes.Helpers
{
    [PublicAPI]
    public static class IDistributedLockTokenExtensions
    {
        public static OperationContextToken CreateOperationContextToken(this IDistributedLockToken token) =>
            CreateOperationContextToken(token.Id);

        internal static OperationContextToken CreateOperationContextToken(this Guid tokenId) =>
            new OperationContextToken($"Lock-{tokenId.ToString("N").Substring(0, 8)}");
    }
}