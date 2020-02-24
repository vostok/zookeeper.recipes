using System;
using JetBrains.Annotations;
using Vostok.ZooKeeper.Client.Abstractions;

namespace Vostok.ZooKeeper.Recipes
{
    /// <summary>
    /// Represents the configuration of a <see cref="DistributedLock"/> instance.
    /// </summary>
    [PublicAPI]
    public class DistributedLockSettings
    {
        /// <param name="path">See <see cref="Path"/>.</param>
        public DistributedLockSettings([NotNull] string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));

            if (path == ZooKeeperPath.Root)
                throw new ArgumentException($"Distributed lock folder '{path}' can't be root.");
        }

        /// <summary>
        /// ZooKeeper path of a designated lock parent node under which lock contenders create their ephemeral nodes.
        /// </summary>
        [NotNull]
        public string Path { get; }
    }
}