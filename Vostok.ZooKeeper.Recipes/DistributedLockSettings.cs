using System;
using JetBrains.Annotations;
using Vostok.ZooKeeper.Client.Abstractions;

namespace Vostok.ZooKeeper.Recipes
{
    /// <summary>
    /// Represents a <see cref="DistributedLock"/> settings.
    /// </summary>
    [PublicAPI]
    public class DistributedLockSettings
    {
        public DistributedLockSettings([NotNull] string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));

            if (path == ZooKeeperPath.Root)
                throw new ArgumentException($"Distributed lock folder '{path}' can't be root.");
        }

        /// <summary>
        /// ZooKeeper path to lock nodes.
        /// </summary>
        [NotNull]
        public string Path { get; }
    }
}