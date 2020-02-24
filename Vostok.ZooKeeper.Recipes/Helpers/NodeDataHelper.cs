using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Vostok.Commons.Binary;
using Vostok.Commons.Environment;

namespace Vostok.ZooKeeper.Recipes.Helpers
{
    internal static class NodeDataHelper
    {
        private const string KeyValueDelimiter = " = ";
        private const string LinesDelimiter = "\n";

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
        private static byte[] Serialize([CanBeNull] IReadOnlyDictionary<string, string> properties)
        {
            properties = properties ?? new Dictionary<string, string>();
            var content = string.Join(
                LinesDelimiter,
                properties
                    .Where(item => !string.IsNullOrEmpty(item.Value))
                    .Select(item => $"{item.Key}{KeyValueDelimiter}{item.Value}".Replace(LinesDelimiter, " ")));
            return Encoding.UTF8.GetBytes(content);
        }

        [NotNull]
        public static Dictionary<string, string> Deserialize([CanBeNull] byte[] data)
        {
            var content = Encoding.UTF8.GetString(data ?? new byte[0]);
            var lines = content.Split(new[] { LinesDelimiter }, StringSplitOptions.RemoveEmptyEntries);
            return lines
                .Where(line => !string.IsNullOrEmpty(line))
                .Select(line => line.Split(new[] { KeyValueDelimiter }, 2, StringSplitOptions.RemoveEmptyEntries))
                .Where(lineParts => lineParts.Length == 2)
                .ToDictionary(
                    lineParts => lineParts[0],
                    lineParts => lineParts[1],
                    StringComparer.OrdinalIgnoreCase
                );
        }
    }
}