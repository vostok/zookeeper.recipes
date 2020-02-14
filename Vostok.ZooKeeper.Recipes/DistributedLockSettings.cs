using System;
using JetBrains.Annotations;

namespace Vostok.ZooKeeper.Recipes
{
    [PublicAPI]
    public class DistributedLockSettings
    {
        public DistributedLockSettings([NotNull] string path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));

            if (!path.EndsWith("/"))
                throw new ArgumentException($"Distributed lock path '{path}' must ends with a slash symbol.");
        }

        [NotNull]
        public string Path { get; }
    }
}