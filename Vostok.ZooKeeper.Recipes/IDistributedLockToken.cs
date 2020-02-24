﻿using System;
using JetBrains.Annotations;
using Vostok.Logging.Context;

namespace Vostok.ZooKeeper.Recipes
{
    /// <summary>
    /// <para><see cref="IDistributedLockToken"/> represents the lifetime of an acquired lock.</para>
    /// <para>Should be periodically checked to ensure whether the lock token is still held by inspecting <see cref="IsAcquired"/> property.</para>
    /// <para>Should be disposed in order to release the lock.</para>
    /// </summary>
    [PublicAPI]
    public interface IDistributedLockToken : IDisposable
    {
        /// <summary>
        /// <para>Returns whether or not the lock represented by this token is still held.</para>
        /// <para>Value of this property may change from <c>true</c> to <c>false</c> anytime.</para>
        /// <para>A <c>false</c> value may also indicate a false negative (in cases such as connection loss).</para>
        /// </summary>
        bool IsAcquired { get; }

        // CR(iloktionov): I strongly suggest to remove this. We could provide a string token id property instead, but this looks like an assumption made for the user.

        /// <summary>
        /// Creates new <see cref="OperationContextToken"/> with current lock id.
        /// </summary>
        IDisposable GetContextToken();
    }
}