using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace EconomySim.Protocols
{
    public record SchemaInfo(string FileName, string Checksum, int Version);

    /// <summary>
    /// Registry holding known schema checksums so tests can verify generated
    /// classes are up to date with their .fbs definitions.
    /// </summary>
    public static class SchemaRegistry
    {
        private static readonly SchemaInfo[] Schemas = new[]
        {
            new SchemaInfo("EconomyUpdatedEvent.fbs", "09b1b2596921e25d39cab761f78f82c7967b279a1c05aaa98e25c4e351f295a0", 1),
            new SchemaInfo("PopulationUpdatedEvent.fbs", "d1dcfc421f31aa142d81bedc73c06601f9b90cc924f69f6e2e21c82b40dac8f1", 1)
        };

        public static IEnumerable<SchemaInfo> All => Schemas.AsEnumerable();

        public static void ValidateCurrentSchemas(string schemaDirectory = "Protocols")
        {
            foreach (var schema in Schemas)
            {
                var path = Path.Combine(schemaDirectory, schema.FileName);
                if (!File.Exists(path))
                    throw new FileNotFoundException($"Schema file not found: {path}");

                var checksum = ComputeChecksum(path);
                if (!checksum.Equals(schema.Checksum, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Schema {schema.FileName} checksum mismatch. Expected {schema.Checksum} but found {checksum}. Run generate_flatbuffers to update generated files.");

                var generated = Path.Combine("Generated", "EconomySim", "Protocols",
                    Path.GetFileNameWithoutExtension(schema.FileName) + ".cs");
                if (!File.Exists(generated))
                    throw new FileNotFoundException($"Generated file missing for {schema.FileName}: {generated}");
            }
        }

        private static string ComputeChecksum(string filePath)
        {
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            var hash = sha.ComputeHash(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
