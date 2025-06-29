using MaxRev.Gdal.Core;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GdalDriver = OSGeo.GDAL.Driver;
using OgrDriver = OSGeo.OGR.Driver;

namespace StrategyGame
{
    /// <summary>
    /// Provides overlay rendering for Natural Earth state borders and city icons.
    /// This class does not modify the base tile generation but draws additional
    /// layers on top of generated terrain tiles.
    /// </summary>
    public static class NaturalEarthOverlayGenerator
    {
        private static readonly Random _random = new Random();
        private static readonly object GdalLock = new object();
        private static bool _gdalConfigured = false;

        private static DataSource _admin1Ds;
        private static Layer _admin1Layer;
        private static DataSource _cityDs;
        private static Layer _cityLayer;

        // Cache for urban masks at different resolutions
        private static readonly Dictionary<(int width, int height), bool[,]> _urbanMaskCache = new();
        private static readonly object _urbanMaskLock = new();

        // NEW: Cache for state masks to avoid regenerating them
        private static readonly ConcurrentDictionary<(int width, int height, int offsetX, int offsetY), int[,]> _stateMaskCache = new();

        // NEW: Cache for country tint masks
        private static readonly ConcurrentDictionary<(int width, int height, int offsetX, int offsetY), int[,]> _countryTintCache = new();

        // Paths follow the same resolution logic as PixelMapGenerator
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

            // first check explicit mappings from DataFileNames
            if (DataFiles.TryGetValue(name, out var mapped) && File.Exists(mapped))
                return mapped;

            // search the user Documents data directory recursively
            string userPath = Path.Combine(DataDir, name);
            if (File.Exists(userPath))
                return userPath;

            if (Directory.Exists(DataDir))
            {
                var matches = Directory.GetFiles(DataDir, name, SearchOption.AllDirectories);
                if (matches.Length > 0)
                    return matches[0];
            }


            // fall back to the repository data directory if nothing found
            string repoPath = Path.Combine(RepoDataDir, name);
            if (File.Exists(repoPath))
                return repoPath;

            if (Directory.Exists(RepoDataDir))
            {
                var matches = Directory.GetFiles(RepoDataDir, name, SearchOption.AllDirectories);
                if (matches.Length > 0)
                    return matches[0];
            }


            // return the path in the Documents folder even if missing so callers know where it was expected
            return userPath;
        }

        private static readonly string TerrainTifPath = GetDataFile("NE1_HR_LC.tif");
        private static readonly string Admin1Path = GetDataFile("ne_10m_admin_1_states_provinces.shp");
        public static readonly string CitiesPath = GetDataFile("ne_10m_populated_places.shp");
        public static readonly string UrbanAreasShpPath = GetDataFile("ne_10m_urban_areas.shp");
        private static readonly Dictionary<int, Rgba32> StateColors = new();
        private static readonly Dictionary<int, Rgba32> CountryColors = new();

        public static bool[,] UrbanMask { get; private set; }

        public static Task InitializeUrbanMaskAsync(int width, int height)
        {
            return Task.Run(() => UrbanMask = CreateUrbanMask(UrbanAreasShpPath, width, height));
        }

        public static Task InitialiseUrbanMaskAsync(int widthCells, int heightCells)
        {
            return Task.Run(() =>
            {
                if (UrbanMask != null) return;
                UrbanMask = CreateUrbanMask(UrbanAreasShpPath, widthCells, heightCells);
            });
        }

        private static Rgba32 GetStateColor(int code)
        {
            if (!StateColors.TryGetValue(code, out var color))
            {
                var rng = new Random(code);
                // Increased alpha from 60 to 90 for more visible province tints
                color = new Rgba32((byte)rng.Next(40, 200), (byte)rng.Next(40, 200), (byte)rng.Next(40, 200), 120);
                StateColors[code] = color;
            }
            return color;
        }

        private static Rgba32 GetCountryColor(int code)
        {
            if (!CountryColors.TryGetValue(code, out var color))
            {
                var rng = new Random(code * 997);
                // Increased alpha from 40 to 70 for more visible country tints
                color = new Rgba32((byte)rng.Next(40, 200), (byte)rng.Next(40, 200), (byte)rng.Next(40, 200), 100);
                CountryColors[code] = color;
            }
            return color;
        }

        /// <summary>
        /// Apply all overlays (state borders and cities) on the provided tile image.
        /// FIXED: Properly handle partial tiles by using actual image dimensions
        /// </summary>
        public static void ApplyOverlays(Image<Rgba32> img, int mapWidth, int mapHeight,
                                         int cellSize, int tileX, int tileY, float zoom,
                                         int tileSizePx = 512)
        {
            // Use the actual image dimensions instead of the expected tile size
            // This is crucial for edge tiles which may be smaller than 512px
            int actualWidthPx = img.Width;
            int actualHeightPx = img.Height;
            
            int mapWidthPx = mapWidth * cellSize;
            int mapHeightPx = mapHeight * cellSize;
            int offsetX = tileX * 512; // Always use standard tile size for offset calculation
            int offsetY = tileY * 512; // Always use standard tile size for offset calculation

            // Guard against off-map tiles
            if (actualWidthPx <= 0 || actualHeightPx <= 0)
                return;

            // Debug output for partial tiles
            if (actualWidthPx != 512 || actualHeightPx != 512)
            {
                Console.WriteLine($"[OVERLAY DEBUG] Applying overlays to partial tile ({tileX},{tileY}): actual={actualWidthPx}x{actualHeightPx}, offset=({offsetX},{offsetY})");
            }

            lock (GdalLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    Gdal.AllRegister();
                    Ogr.RegisterAll();
                    _gdalConfigured = true;
                }

                if (_admin1Ds == null && File.Exists(Admin1Path))
                {
                    _admin1Ds = Ogr.Open(Admin1Path, 0);
                    _admin1Layer = _admin1Ds.GetLayerByIndex(0);
                }
                if (_cityDs == null && File.Exists(CitiesPath))
                {
                    _cityDs = Ogr.Open(CitiesPath, 0);
                    _cityLayer = _cityDs.GetLayerByIndex(0);
                }
            }

            // Use actual image dimensions instead of calculated dimensions
            TintCountries(img, mapWidth, mapHeight, cellSize, tileX, tileY, actualWidthPx, actualHeightPx, offsetX, offsetY);
            DrawStateBorders(img, mapWidth, mapHeight, cellSize, tileX, tileY, actualWidthPx, actualHeightPx, offsetX, offsetY);
        }

        private static void DrawStateBorders(Image<Rgba32> img, int mapWidth, int mapHeight,
                                             int cellSize, int tileX, int tileY, 
                                             int actualWidthPx, int actualHeightPx, 
                                             int offsetX, int offsetY)
        {
            if (_admin1Layer == null)
                return;

            // Guard against off-map tiles
            if (actualWidthPx <= 0 || actualHeightPx <= 0)
                return;

            // NEW: Use cached state mask with actual dimensions
            var maskKey = (actualWidthPx, actualHeightPx, offsetX, offsetY);
            int[,] mask;
            
            if (!_stateMaskCache.TryGetValue(maskKey, out mask))
            {
                int mapWidthPx = mapWidth * cellSize;
                int mapHeightPx = mapHeight * cellSize;
                mask = CreateMaskTile(_admin1Layer, "adm1_code", offsetX, offsetY, actualWidthPx, actualHeightPx, mapWidthPx, mapHeightPx);
                
                // Only cache if not too many cached (prevent memory bloat)
                if (_stateMaskCache.Count < 500)
                {
                    _stateMaskCache.TryAdd(maskKey, mask);
                }
            }
            
            Rgba32 borderColor = new Rgba32(255, 255, 255, 180);

            img.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < actualHeightPx; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    for (int x = 0; x < actualWidthPx; x++)
                    {
                        int v = mask[y, x];
                        if (v == 0) continue;
                        Rgba32 tint = GetStateColor(v);
                        float a = tint.A / 255f;
                        Rgba32 basePix = row[x];
                        basePix.R = (byte)(basePix.R * (1 - a) + tint.R * a);
                        basePix.G = (byte)(basePix.G * (1 - a) + tint.G * a);
                        basePix.B = (byte)(basePix.B * (1 - a) + tint.B * a);
                        row[x] = basePix;
                    }
                }

                for (int y = 1; y < actualHeightPx - 1; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    for (int x = 1; x < actualWidthPx - 1; x++)
                    {
                        int v = mask[y, x];
                        if (v == 0) continue;
                        if (mask[y - 1, x] != v || mask[y + 1, x] != v || mask[y, x - 1] != v || mask[y, x + 1] != v)
                        {
                            row[x] = borderColor;
                        }
                    }
                }
            });
        }

        /*
        private static void DrawCities(Image<Rgba32> img,
                               int mapWidth, int mapHeight,
                               int cellSize, int tileX, int tileY,
                               float zoom,
                               int tileSizePx = 512)
        {
            if (_cityLayer == null) return;

            // global map size in *screen* pixels
            int mapWidthPx = mapWidth * cellSize;
            int mapHeightPx = mapHeight * cellSize;

            // where this tile starts in the global image
            int offsetX = tileX * tileSizePx;
            int offsetY = tileY * tileSizePx;

            // build a lon/lat bounding box for a quick spatial filter
            double west = -180.0 + 360.0 * offsetX / mapWidthPx;
            double east = west + 360.0 * img.Width / mapWidthPx;
            double north = 90.0 - 180.0 * offsetY / mapHeightPx;
            double south = north - 180.0 * img.Height / mapHeightPx;
            _cityLayer.SetSpatialFilterRect(west, south, east, north);

            Feature f;
            _cityLayer.ResetReading();
            while ((f = _cityLayer.GetNextFeature()) != null)
            {
                var g = f.GetGeometryRef();
                if (g == null || g.GetGeometryType() != wkbGeometryType.wkbPoint) { f.Dispose(); continue; }

                double lon = g.GetX(0), lat = g.GetY(0);   // WGS-84
                (int gx, int gy) = LonLatToPixel(lon, lat, mapWidthPx, mapHeightPx);

                int px = gx - offsetX;
                int py = gy - offsetY;
                if (px < 0 || py < 0 || px >= img.Width || py >= img.Height) { f.Dispose(); continue; }

                try
                {
                    int pop = f.GetFieldAsInteger("POP_MAX");
                    int size = pop > 500_000 ? 4 : pop > 100_000 ? 3 : 2;
                    var col = pop > 1_000_000 ? new Rgba32(255, 215, 0) : new Rgba32(200, 50, 50);
                    DrawSquare(img, px - size / 2, py - size / 2, size, col);
                }
                catch (Exception ex)
                {
                    // Handle column access errors gracefully
                    Console.WriteLine($"[WARNING] Error reading city data: {ex.Message}");
                    DrawSquare(img, px - 2, py - 2, 4, new Rgba32(200, 50, 50));
                }

                f.Dispose();
            }
            _cityLayer.SetSpatialFilter(null);
        }
        */

        private static (int x, int y) LonLatToPixel(double lon, double lat,
                                            int mapWidthPx, int mapHeightPx)
        {
            int x = (int)Math.Round((lon + 180.0) / 360.0 * mapWidthPx);
            int y = (int)Math.Round((90.0 - lat) / 180.0 * mapHeightPx);
            return (x, y);
        }

        private static void TintCountries(Image<Rgba32> img, int mapWidth, int mapHeight,
                                           int cellSize, int tileX, int tileY, 
                                           int actualWidthPx, int actualHeightPx, 
                                           int offsetX, int offsetY)
        {
            // Guard against off-map tiles
            if (actualWidthPx <= 0 || actualHeightPx <= 0)
                return;

            // NEW: Use cached country tint mask with actual dimensions
            var maskKey = (actualWidthPx, actualHeightPx, offsetX, offsetY);
            int[,] mask;
            
            if (!_countryTintCache.TryGetValue(maskKey, out mask))
            {
                int mapWidthPx = mapWidth * cellSize;
                int mapHeightPx = mapHeight * cellSize;
                mask = PixelMapGenerator.CreateCountryMaskTile(mapWidthPx, mapHeightPx,
                    offsetX, offsetY, actualWidthPx, actualHeightPx);
                    
                // Only cache if not too many cached (prevent memory bloat)
                if (_countryTintCache.Count < 500)
                {
                    _countryTintCache.TryAdd(maskKey, mask);
                }
            }

            img.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < actualHeightPx; y++)
                {
                    Span<Rgba32> row = accessor.GetRowSpan(y);
                    for (int x = 0; x < actualWidthPx; x++)
                    {
                        int v = mask[y, x];
                        if (v == 0) continue;  // Skip water pixels (mask value 0) - keep LightSkyBlue oceans untouched
                        
                        Rgba32 tint = GetCountryColor(v);
                        float a = tint.A / 255f;
                        Rgba32 basePix = row[x];
                        basePix.R = (byte)(basePix.R * (1 - a) + tint.R * a);
                        basePix.G = (byte)(basePix.G * (1 - a) + tint.G * a);
                        basePix.B = (byte)(basePix.B * (1 - a) + tint.B * a);
                        row[x] = basePix;
                    }
                }
            });
        }

        private static int[,] CreateMaskTile(Layer layer, string attr, int offsetX, int offsetY, int width, int height, int fullWidth, int fullHeight)
        {
            if (fullWidth == 0 || fullHeight == 0 || width <= 0 || height <= 0)
                return new int[Math.Max(1, height), Math.Max(1, width)];

            using var dem = Gdal.Open(TerrainTifPath, Access.GA_ReadOnly);
            double[] gt = new double[6];
            dem.GetGeoTransform(gt);
            int srcCols = dem.RasterXSize;
            int srcRows = dem.RasterYSize;

            // FIXED: Use the same coordinate system as PixelMapGenerator
            double worldWidth = gt[1] * srcCols;   
            double worldHeight = Math.Abs(gt[5]) * srcRows; 
            
            double pixelSizeX = worldWidth / fullWidth;   
            double pixelSizeY = worldHeight / fullHeight; 
            
            double tileWorldX = gt[0] + offsetX * pixelSizeX;
            double tileWorldY = gt[3] - offsetY * pixelSizeY;

            double[] newGt = new double[6];
            newGt[0] = tileWorldX;        
            newGt[1] = pixelSizeX;        
            newGt[2] = 0;                 
            newGt[3] = tileWorldY;        
            newGt[4] = 0;                 
            newGt[5] = -pixelSizeY;       

            GdalDriver memDrv = Gdal.GetDriverByName("MEM");
            using var maskDs = memDrv.Create("", width, height, 1, DataType.GDT_Int32, null);
            maskDs.SetGeoTransform(newGt);
            maskDs.SetProjection(dem.GetProjection());

            // Set spatial filter based on the actual geographic bounds
            double left = tileWorldX;
            double right = tileWorldX + width * pixelSizeX;
            double top = tileWorldY;
            double bottom = tileWorldY - height * pixelSizeY;
            
            layer.SetSpatialFilterRect(left, bottom, right, top);

            Gdal.RasterizeLayer(maskDs, 1, new[] { 1 }, layer, IntPtr.Zero, IntPtr.Zero,
                0, null, new[] { $"ATTRIBUTE={attr}" }, null, "");
            layer.SetSpatialFilter(null);

            Band band = maskDs.GetRasterBand(1);
            int[] flat = new int[width * height];
            band.ReadRaster(0, 0, width, height, flat, width, height, 0, 0);

            int[,] result = new int[height, width];
            for (int r = 0; r < height; r++)
                for (int c = 0; c < width; c++)
                    result[r, c] = flat[r * width + c];

            return result;
        }

        private static void DrawSquare(Image<Rgba32> img, int x, int y, int size, Rgba32 color)
        {
            img.ProcessPixelRows(accessor =>
            {
                for (int yy = 0; yy < size; yy++)
                {
                    int py = y + yy;
                    if (py < 0 || py >= img.Height) continue;
                    Span<Rgba32> row = accessor.GetRowSpan(py);
                    for (int xx = 0; xx < size; xx++)
                    {
                        int px = x + xx;
                        if (px < 0 || px >= img.Width) continue;
                        row[px] = color;
                    }
                }
            });
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
                        dict[trimmed] = userPath;
                    else
                        dict[trimmed] = Path.Combine(RepoDataDir, trimmed);
                }
            }
            return dict;
        }

        /// <summary>
        /// Creates a boolean mask indicating urban areas for fast pixel-art rendering.
        /// This is cached and reused across multiple tile generations.
        /// </summary>
        public static bool[,] CreateUrbanMask(string shpPath, int width, int height)
        {
            var key = (width, height);
            
            lock (_urbanMaskLock)
            {
                if (_urbanMaskCache.TryGetValue(key, out var cached))
                    return cached;
            }

            Console.WriteLine($"[DEBUG] Creating urban mask for {width}x{height}");
            
            if (!File.Exists(shpPath))
            {
                Console.WriteLine($"[WARNING] Urban areas shapefile not found: {shpPath}");
                var emptyMask = new bool[height, width];
                lock (_urbanMaskLock)
                {
                    _urbanMaskCache[key] = emptyMask;
                }
                return emptyMask;
            }

            bool[,] urbanMask = new bool[height, width];

            try
            {
                lock (GdalLock)
                {
                    if (!_gdalConfigured)
                    {
                        GdalBase.ConfigureAll();
                        Gdal.AllRegister();
                        Ogr.RegisterAll();
                        _gdalConfigured = true;
                    }

                    using var dem = Gdal.Open(TerrainTifPath, Access.GA_ReadOnly);
                    double[] gt = new double[6];
                    dem.GetGeoTransform(gt);

                    // Set up geotransform for the urban mask
                    double[] newGt = new double[6];
                    newGt[0] = gt[0];  // top-left x
                    newGt[1] = gt[1] * dem.RasterXSize / width;   // pixel width
                    newGt[2] = 0;      // rotation
                    newGt[3] = gt[3];  // top-left y
                    newGt[4] = 0;      // rotation
                    newGt[5] = gt[5] * dem.RasterYSize / height;  // pixel height

                    GdalDriver memDrv = Gdal.GetDriverByName("MEM");
                    using var maskDs = memDrv.Create("", width, height, 1, DataType.GDT_Byte, null);
                    maskDs.SetGeoTransform(newGt);
                    maskDs.SetProjection(dem.GetProjection());

                    using var ds = Ogr.Open(shpPath, 0);
                    var layer = ds.GetLayerByIndex(0);

                    // Rasterize urban areas as value 1
                    Gdal.RasterizeLayer(maskDs, 1, new[] { 1 }, layer, IntPtr.Zero, IntPtr.Zero,
                        0, null, new[] { "BURN_VALUE=1" }, null, "");

                    // Read the rasterized data
                    var band = maskDs.GetRasterBand(1);
                    byte[] buffer = new byte[width * height];
                    band.ReadRaster(0, 0, width, height, buffer, width, height, 0, 0);

                    // Convert to bool array
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            urbanMask[y, x] = buffer[y * width + x] == 1;
                        }
                    }
                }

                lock (_urbanMaskLock)
                {
                    _urbanMaskCache[key] = urbanMask;
                }

                Console.WriteLine($"[DEBUG] Urban mask created successfully for {width}x{height}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to create urban mask: {ex.Message}");
                lock (_urbanMaskLock)
                {
                    _urbanMaskCache[key] = urbanMask; // Cache empty mask to avoid repeated failures
                }
            }

            return urbanMask;
        }

        /// <summary>
        /// Gets or creates urban mask for the specified map dimensions
        /// </summary>
        public static bool[,] GetUrbanMask(int mapWidth, int mapHeight)
        {
            if (UrbanMask != null && UrbanMask.GetLength(1) == mapWidth && UrbanMask.GetLength(0) == mapHeight)
            {
                return UrbanMask;
            }
            return CreateUrbanMask(UrbanAreasShpPath, mapWidth, mapHeight);
        }

        /// <summary>
        /// Clears the urban mask cache to free memory or force regeneration
        /// </summary>
        public static void ClearUrbanMaskCache()
        {
            lock (_urbanMaskLock)
            {
                _urbanMaskCache.Clear();
                Console.WriteLine("[DEBUG] Urban mask cache cleared");
            }
        }

        /// <summary>
        /// Gets the current size of the urban mask cache
        /// </summary>
        public static int GetUrbanMaskCacheSize()
        {
            lock (_urbanMaskLock)
            {
                return _urbanMaskCache.Count;
            }
        }

        /// <summary>
        /// NEW: Clear all overlay caches to free memory
        /// </summary>
        public static void ClearAllOverlayCaches()
        {
            ClearUrbanMaskCache();
            _stateMaskCache.Clear();
            _countryTintCache.Clear();
            Console.WriteLine("[DEBUG] All overlay caches cleared");
        }

        /// <summary>
        /// NEW: Get cache statistics for monitoring
        /// </summary>
        public static (int urbanMasks, int stateMasks, int countryTints) GetOverlayCacheStats()
        {
            int urbanCount;
            lock (_urbanMaskLock)
            {
                urbanCount = _urbanMaskCache.Count;
            }
            return (urbanCount, _stateMaskCache.Count, _countryTintCache.Count);
        }
    }
}

