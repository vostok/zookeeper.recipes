using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.ZooKeeper.Recipes.Helpers;

namespace Vostok.ZooKeeper.Recipes
{
    /// <summary>
    /// <para>An instance of type <see cref="IDistributedLock"/> is an entry point for acquiring distributed lock.</para>
    /// <para>See <see cref="AcquireAsync"/>, <see cref="TryAcquireAsync"/> and <see cref="DistributedLockToken"/> for details.</para>
    /// </summary>
    [PublicAPI]
    public interface IDistributedLock
    {
        /// <summary>
        /// <para>Tries to acquire distributed lock.</para>
        /// <para>Returns null, if timeout expired.</para>
        /// <para>Otherwise, returns <see cref="DistributedLockToken"/> that should be disposed after use.</para>
        /// <para>Throws an exception if cancellation has been requested, or non-retryable error has occured.</para>
        /// </summary>
        [CanBeNull]
        Task<IDistributedLockToken> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// <para>Acquires distributed lock.</para>
        /// <para>Returns <see cref="DistributedLockToken"/> that should be disposed after use.</para>
        /// <para>Throws an exception if cancellation has been requested, or non-retryable error has occured.</para>
        /// </summary>
        [NotNull]
        Task<IDistributedLockToken> AcquireAsync(CancellationToken cancellationToken = default);
    }
}