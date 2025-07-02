using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using System;
using System.Collections.Generic;
using System.IO;

namespace StrategyGame
{
    public static class UrbanAreaManager
    {
        public static List<Polygon> UrbanPolygons { get; private set; } = new();

        private static readonly string RepoRoot =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data");
        private static readonly string RepoDataDir = Path.Combine(RepoRoot, "data");
        private static readonly string DataFileList = Path.Combine(RepoRoot, "DataFileNames");
        private static readonly Dictionary<string, string> DataFiles = LoadDataFiles();

        public static void LoadUrbanAreas()
        {
            UrbanPolygons.Clear();
            string shp = GetDataFile("ne_10m_urban_areas.shp");
            if (!File.Exists(shp))
                return;

            var reader = new ShapefileDataReader(shp, GeometryFactory.Default);
            while (reader.Read())
            {
                var geom = reader.Geometry;
                if (geom is MultiPolygon mp)
                {
                    for (int i = 0; i < mp.NumGeometries; i++)
                    {
                        if (mp.GetGeometryN(i) is Polygon p)
                            UrbanPolygons.Add(p);
                    }
                }
                else if (geom is Polygon p)
                {
                    UrbanPolygons.Add(p);
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
