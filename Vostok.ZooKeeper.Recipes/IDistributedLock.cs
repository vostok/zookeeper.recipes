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
        /// <para>Attempts to acquire a distributed lock asynchronously.</para>
        /// <para>Returns null if given <paramref name="timeout"/> is insufficient to obtain the lock.</para>
        /// <para>Otherwise, returns a <see cref="DistributedLockToken"/> that should be disposed after use.</para>
        /// <para>Throws an exception in the event of cancellation or a non-retriable error.</para>
        /// </summary>
        [ItemCanBeNull]
        Task<IDistributedLockToken> TryAcquireAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

        /// <summary>
        /// <para>Acquires a distributed lock asynchronously.</para>
        /// <para>Returns a <see cref="DistributedLockToken"/> that should be disposed after use.</para>
        /// <para>Throws an exception in the event of cancellation or a non-retriable error.</para>
        /// </summary>
        [ItemNotNull]
        Task<IDistributedLockToken> AcquireAsync(CancellationToken cancellationToken = default);
    }
}