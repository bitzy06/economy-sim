using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StrategyGame
{
    /// <summary>
    /// Validates that FlatBuffers schema versions match the registry.
    /// </summary>
    public static class SchemaValidator
    {
        private const string RegistryFile = "schema_registry.json";

        public static void Validate()
        {
            if (!File.Exists(RegistryFile))
                throw new FileNotFoundException($"Schema registry '{RegistryFile}' not found.");

            var registry = JsonSerializer.Deserialize<Dictionary<string, ushort>>(File.ReadAllText(RegistryFile))
                           ?? new Dictionary<string, ushort>();

            foreach (var fbs in Directory.GetFiles("FlatBuffersSchemas", "*.fbs"))
            {
                var name = Path.GetFileNameWithoutExtension(fbs);
                var text = File.ReadAllText(fbs);
                var match = Regex.Match(text, @"version:ushort\s*=\s*(\d+)");
                if (!match.Success)
                    continue;
                var version = ushort.Parse(match.Groups[1].Value);

                if (!registry.TryGetValue(name, out var expected) || expected != version)
                    throw new InvalidOperationException($"Schema version mismatch for {name}: expected {expected}, found {version}");
            }
        }
    }
}
