using System;
using JetBrains.Annotations;
using Vostok.Logging.Context;

namespace Vostok.ZooKeeper.Recipes
{
    /// <summary>
    /// <para>Represents a distributed lock token.</para>
    /// <para>Should be periodically checked, whether or not this lock token is still alive, by calling <see cref="IsAcquired"/>.</para>
    /// <para>Call <see cref="IDisposable.Dispose"/> to release lock.</para>
    /// </summary>
    [PublicAPI]
    public interface IDistributedLockToken : IDisposable
    {
        /// <summary>
        /// <para>Returns whether or not this lock token is still alive.</para>
        /// </summary>
        bool IsAcquired { get; }

        /// <summary>
        /// Creates new <see cref="OperationContextToken"/> with current lock id.
        /// </summary>
        IDisposable GetLockContextToken();
    }
}