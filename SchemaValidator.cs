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
        private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

        private static readonly string RegistryPath = Path.Combine(RepoRoot, "schema_registry.json");

        public static void Validate()
        {
            if (!File.Exists(RegistryPath))
                throw new FileNotFoundException($"Schema registry '{RegistryPath}' not found.");

            var registry = JsonSerializer.Deserialize<Dictionary<string, ushort>>(File.ReadAllText(RegistryPath))
                           ?? new Dictionary<string, ushort>();

            var schemaDir = Path.Combine(RepoRoot, "FlatBuffersSchemas");
            if (!Directory.Exists(schemaDir))
                return;

            foreach (var fbs in Directory.GetFiles(schemaDir, "*.fbs"))
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
