using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace StrategyGame
{
    public static class CountryBorderManager
    {
        private static readonly Dictionary<string, Geometry> countryPolygons = new();
        private static readonly List<string> countryNames = new();

        private static readonly string RepoRoot =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data");
        private static readonly string RepoDataDir = Path.Combine(RepoRoot, "data");
        private static readonly string DataFileList = Path.Combine(RepoRoot, "DataFileNames");
        private static readonly Dictionary<string, string> DataFiles = LoadDataFiles();

        public static IEnumerable<string> CountryNames
        {
            get
            {
                EnsureLoaded();
                return countryNames;
            }
        }

        public static void EnsureLoaded()
        {
            if (countryNames.Count > 0 || countryPolygons.Count > 0)
                return;

            string shp = GetDataFile("ne_10m_admin_0_countries.shp");
            if (File.Exists(shp))
            {
                var reader = new ShapefileDataReader(shp, GeometryFactory.Default);
                int nameIndex = -1;
                var fields = reader.DbaseHeader.Fields;
                for (int i = 0; i < fields.Length; i++)
                {
                    var fname = fields[i].Name;
                    if (string.Equals(fname, "ADMIN", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(fname, "NAME", StringComparison.OrdinalIgnoreCase))
                    {
                        nameIndex = i;
                        break;
                    }
                }
                if (nameIndex == -1)
                    nameIndex = 0;

                while (reader.Read())
                {
                    string name = reader.GetString(nameIndex);
                    Geometry geom = reader.Geometry;
                    if (geom != null && !countryPolygons.ContainsKey(name))
                    {
                        countryPolygons[name] = geom;
                        countryNames.Add(name);
                    }
                }
                return;
            }

            // Fallback: if shapefile missing, try world_setup.json for names
            string setupPath = Path.Combine(RepoRoot, "world_setup.json");
            if (File.Exists(setupPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(setupPath));
                    foreach (var c in doc.RootElement.GetProperty("Countries").EnumerateArray())
                    {
                        if (c.TryGetProperty("Name", out var n))
                        {
                            countryNames.Add(n.GetString());
                        }
                    }
                }
                catch { }
            }
        }

        public static IEnumerable<Polygon> FilterUrbanAreasByCountries(IEnumerable<Polygon> areas, IEnumerable<string> countries)
        {
            EnsureLoaded();
            var set = new HashSet<string>(countries);
            if (countryPolygons.Count == 0)
            {
                foreach (var a in areas)
                    yield return a;
                yield break;
            }
            foreach (var area in areas)
            {
                var centroid = area.Centroid;
                foreach (var kvp in countryPolygons)
                {
                    if (!set.Contains(kvp.Key))
                        continue;
                    if (kvp.Value.Contains(centroid))
                    {
                        yield return area;
                        break;
                    }
                }
            }
        }

        private static string GetDataFile(string name)
        {
            if (DataFiles.TryGetValue(name, out var mapped) && File.Exists(mapped))
                return mapped;

            string userPath = Path.Combine(DataDir, name);
            if (File.Exists(userPath))
                return userPath;

            if (Directory.Exists(DataDir))
            {
                var matches = Directory.GetFiles(DataDir, name, SearchOption.AllDirectories);
                if (matches.Length > 0)
                    return matches[0];
            }

            string repoPath = Path.Combine(RepoDataDir, name);
            if (File.Exists(repoPath))
                return repoPath;

            if (Directory.Exists(RepoDataDir))
            {
                var matches = Directory.GetFiles(RepoDataDir, name, SearchOption.AllDirectories);
                if (matches.Length > 0)
                    return matches[0];
            }

            return userPath;
        }

        private static Dictionary<string, string> LoadDataFiles()
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(DataFileList))
            {
                foreach (var line in File.ReadAllLines(DataFileList))
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("files"))
                        continue;

                    string userPath = Path.Combine(DataDir, trimmed);
                    if (File.Exists(userPath))
                    {
                        dict[trimmed] = userPath;
                    }
                    else
                    {
                        dict[trimmed] = Path.Combine(RepoDataDir, trimmed);
                    }
                }
            }
            return dict;
        }
    }
}
