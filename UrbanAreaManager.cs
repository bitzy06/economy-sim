using Nts = NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.Index.Strtree;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Linq;

namespace StrategyGame
{
    public static class UrbanAreaManager
    {
        public static readonly List<Nts.Polygon> UrbanPolygons;
        private static readonly STRtree<Nts.Polygon> _index;

        static UrbanAreaManager()
        {
            UrbanPolygons = LoadAllUrbanPolygons();
            _index = new STRtree<Nts.Polygon>();
            foreach (var poly in UrbanPolygons)
                _index.Insert(poly.EnvelopeInternal, poly);
            _index.Build();
        }

        private static readonly string RepoRoot =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data");
        private static readonly string RepoDataDir = Path.Combine(RepoRoot, "data");
        private static readonly string DataFileList = Path.Combine(RepoRoot, "DataFileNames");
        private static readonly Dictionary<string, string> DataFiles = LoadDataFiles();

        private static List<Nts.Polygon> LoadAllUrbanPolygons()
        {
            var list = new List<Nts.Polygon>();
            string shp = GetDataFile("ne_10m_urban_areas.shp");
            if (!File.Exists(shp))
                return list;

            var reader = new ShapefileDataReader(shp, Nts.GeometryFactory.Default);
            while (reader.Read())
            {
                var geom = reader.Geometry;
                if (geom is Nts.MultiPolygon mp)
                {
                    for (int i = 0; i < mp.NumGeometries; i++)
                    {
                        if (mp.GetGeometryN(i) is Nts.Polygon p)
                            list.Add(p);
                    }
                }
                else if (geom is Nts.Polygon p)
                {
                    list.Add(p);
                }
            }

            return list;
        }

        public static void PrecomputeAllRoadNetworks()
        {
            Debug.WriteLine("Starting background pre-computation of all urban road networks...");
            var sw = Stopwatch.StartNew();

            Parallel.ForEach(UrbanPolygons, urbanArea =>
            {
                RoadNetworkGenerator.GetOrGenerateFor(urbanArea, 40);
            });

            sw.Stop();
            Debug.WriteLine($"Finished pre-computing all road networks in {sw.Elapsed.TotalSeconds:F2} seconds.");
        }

        public static IEnumerable<Nts.Polygon> Query(GeoBounds bounds)
        {
            var env = new Nts.Envelope(bounds.MinLon, bounds.MaxLon, bounds.MinLat, bounds.MaxLat);
            return _index.Query(env).Cast<Nts.Polygon>();
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
