using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.Commons.Binary;
using Vostok.Commons.Environment;

namespace Vostok.ZooKeeper.Recipes.Helpers
{
    internal static class NodeDataHelper
    {
        public static byte[] GetNodeData()
        {
            return Serialize(
                new Dictionary<string, string>
                {
                    ["hostName"] = EnvironmentInfo.Host,
                    ["application"] = EnvironmentInfo.Application,
                    ["processName"] = EnvironmentInfo.ProcessName,
                    ["processId"] = EnvironmentInfo.ProcessId?.ToString()
                });
        }

        [NotNull]
        public static IReadOnlyDictionary<string, string> Deserialize([CanBeNull] byte[] data)
        {
            if (data == null || data.Length == 0)
                return new Dictionary<string, string>();

            var reader = new BinaryBufferReader(data, 0);

            return reader.ReadDictionary(r => r.ReadString(), r => r.ReadString());
        }

        private static byte[] Serialize([CanBeNull] IReadOnlyDictionary<string, string> dictionary)
        {
            var writer = new BinaryBufferWriter(0);
            writer.WriteDictionary(
                dictionary ?? new Dictionary<string, string>(),
                (w, k) => w.WriteWithLength(k),
                (w, v) => w.WriteWithLength(v));

            return writer.Buffer;
        }
    }
}