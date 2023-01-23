using System;
using System.Threading;
using JetBrains.Annotations;

namespace Vostok.ZooKeeper.Recipes
{
    /// <summary>
    /// <para><see cref="IDistributedLockToken"/> represents the lifetime of an acquired lock.</para>
    /// <para>Should be periodically checked to ensure whether the lock token is still held by inspecting <see cref="IsAcquired"/> or <see cref="CancellationToken"/>.</para>
    /// <para>Should be disposed in order to release the lock.</para>
    /// </summary>
    [PublicAPI]
    public interface IDistributedLockToken
    : IDisposable
    #if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    , IAsyncDisposable
    #endif
    {
        /// <summary>
        /// <para>Returns whether or not the lock represented by this token is still held.</para>
        /// <para>Value of this property may change from <c>true</c> to <c>false</c> anytime.</para>
        /// <para>A <c>false</c> value may also indicate a false negative (in cases such as connection loss).</para>
        /// </summary>
        bool IsAcquired { get; }
        
        /// <summary>
        /// <para>A token that triggers on lock loss.</para>
        /// </summary>
        CancellationToken CancellationToken { get; }
    }
}