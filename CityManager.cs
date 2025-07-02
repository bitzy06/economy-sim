using MaxRev.Gdal.Core;
using OSGeo.OGR;
using System;
using System.Collections.Generic;
using System.IO;

namespace StrategyGame
{
    /// <summary>
    /// Loads procedural city locations and provides lookup utilities.
    /// </summary>
    public static class CityManager
    {
        public static List<ProceduralCity> AllCities { get; private set; } = new List<ProceduralCity>();

        private static readonly object GdalLock = new();
        private static bool _gdalConfigured = false;

        private static readonly string RepoRoot =
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data");

        private static readonly string RepoDataDir = Path.Combine(RepoRoot, "data");
        private static readonly string DataFileList = Path.Combine(RepoRoot, "DataFileNames");
        private static readonly Dictionary<string, string> DataFiles = LoadDataFiles();

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

        public static void LoadCities()
        {
            lock (GdalLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }

            string placesPath = GetDataFile("ne_10m_populated_places.shp");
            if (!File.Exists(placesPath))
                return;

            AllCities.Clear();

            using DataSource ds = Ogr.Open(placesPath, 0);
            Layer layer = ds.GetLayerByIndex(0);
            layer.ResetReading();
            Feature feat;
            while ((feat = layer.GetNextFeature()) != null)
            {
                var geom = feat.GetGeometryRef();
                double lon = geom.GetX(0);
                double lat = geom.GetY(0);
                AllCities.Add(new ProceduralCity(lon, lat));
                feat.Dispose();
            }
        }

        public struct GeoBounds
        {
            public double MinLon;
            public double MaxLon;
            public double MinLat;
            public double MaxLat;
        }

        public static List<ProceduralCity> GetCitiesInBounds(GeoBounds bounds)
        {
            var list = new List<ProceduralCity>();
            foreach (var city in AllCities)
            {
                if (city.WorldX >= bounds.MinLon && city.WorldX <= bounds.MaxLon &&
                    city.WorldY >= bounds.MinLat && city.WorldY <= bounds.MaxLat)
                {
                    list.Add(city);
                }
            }
            return list;
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

