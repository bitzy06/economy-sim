using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace StrategyGame
{
    /// <summary>
    /// Provides helper methods for generating a pixel-art map based on the
    /// ETOPO1 elevation data. The GeoTIFF is downloaded using the existing
    /// Python script when not already present.
    /// </summary>
    public static class PixelMapGenerator
    {
        private static readonly object GdalConfigLock = new object();
        private static bool _gdalConfigured = false;

        // NEW: Cache for full-map masks, near your other cache declarations
        private static readonly ConcurrentDictionary<(int width, int height), int[,]> _fullCountryMaskCache = new();

        // NEW: Cache for country masks to avoid regenerating them
        private static readonly ConcurrentDictionary<(int width, int height, int offsetX, int offsetY), int[,]> _countryMaskCache = new();

        // NEW: Cache for terrain data to avoid repeated GDAL reads
        private static readonly ConcurrentDictionary<(int startX, int startY, int width, int height, int mapSize), (byte[] r, byte[] g, byte[] b)> _terrainDataCache = new();

        // NEW: Pre-loaded urban texture cache
        private static Image<Rgba32> _cachedUrbanTexture;
        private static readonly object _urbanTextureLock = new();

        // NEW: Reusable GDAL data sources
        private static readonly object _dataSourceLock = new();
        private static Dataset _terrainDataset;
        private static DataSource _countryDataSource;

        // NEW: Thread-safe random number generator
        private static readonly ThreadLocal<Random> _threadLocalRandom = new(() => new Random(Environment.TickCount + Thread.CurrentThread.ManagedThreadId));

        // NEW: Memory pool for byte arrays to reduce allocations
        private static readonly ConcurrentQueue<byte[]> _byteArrayPool = new();
        private const int MaxPooledArrays = 100;
        private const int PooledArraySize = 1024 * 1024; // 1MB arrays

        // Resolve paths relative to the repository root so the application does
        // not depend on developer specific locations. The executable lives in
        // bin/Debug or bin/Release so we need to traverse three directories up
        // to reach the repo root and then into the data folder.
        private static readonly string RepoRoot =
            System.IO.Path.GetFullPath(System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));


        // Data files are expected to live in the user's Documents\data folder
        // (e.g. "C:\\Users\\kayla\\Documents\\data").  This path is used directly
        // rather than falling back to the repository so the game always loads
        // external resources from that location.
        private static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data");


        private static readonly string RepoDataDir = Path.Combine(RepoRoot, "data");


        // Data files listed in the text file are resolved relative to the data
        // directory.  This allows the application to load resources that are
        // not part of the repository but exist locally.
        private static readonly string DataFileList =
            Path.Combine(RepoRoot, "DataFileNames");

        private static readonly Dictionary<string, string> DataFiles = LoadDataFiles();

        // System.Drawing fails with "Parameter is not valid" when width or height
        // exceed approximately 32k pixels.  Clamp generated bitmap dimensions to
        // stay below this threshold.
        /// <summary>
        /// Maximum bitmap dimension that can be safely created using
        /// System.Drawing. Larger images must be generated using
        /// ImageSharp to avoid GDI limitations.
        /// </summary>
        public const int MaxBitmapDimension = 30000;

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

        private static readonly string TifPath =
            GetDataFile("ETOPO1_Bed_g_geotiff.tif");
        private static readonly string ShpPath =
            GetDataFile("ne_10m_admin_0_countries.shp");

        // Terrain map used for pixel-art generation.
        private static readonly string TerrainTifPath =
            GetDataFile("NE1_HR_LC.tif");

        // NEW: Urban texture layer for blending with terrain
        private static readonly string UrbanTexturePath = Path.Combine(DataDir, "urban_texture.png");

        /// <summary>
        /// NEW: Initialize caches and preload frequently used data
        /// </summary>
        public static async Task InitializeAsync()
        {
            await Task.Run(() =>
            {
                lock (GdalConfigLock)
                {
                    if (!_gdalConfigured)
                    {
                        GdalBase.ConfigureAll();
                        _gdalConfigured = true;
                    }
                }

                // Preload urban texture if available
                PreloadUrbanTexture();

                // Initialize memory pool
                InitializeMemoryPool();

                // Initialize shared GDAL datasets
                lock (_dataSourceLock)
                {
                    _terrainDataset ??= Gdal.Open(TerrainTifPath, Access.GA_ReadOnly);
                    _countryDataSource ??= Ogr.Open(ShpPath, 0);
                }

                Console.WriteLine("[DEBUG] PixelMapGenerator initialized with caching optimizations");
            });
        }

        /// <summary>
        /// NEW: Preload urban texture to avoid loading it for every tile
        /// </summary>
        private static void PreloadUrbanTexture()
        {
            lock (_urbanTextureLock)
            {
                if (_cachedUrbanTexture != null) return;

                try
                {
                    if (File.Exists(UrbanTexturePath))
                    {
                        _cachedUrbanTexture = SixLabors.ImageSharp.Image.Load<Rgba32>(UrbanTexturePath);
                        Console.WriteLine($"[DEBUG] Urban texture preloaded: {_cachedUrbanTexture.Width}x{_cachedUrbanTexture.Height}");
                    }
                    else
                    {
                        _cachedUrbanTexture = new Image<Rgba32>(1, 1, new Rgba32(0, 0, 0, 0));
                        Console.WriteLine("[DEBUG] Urban texture not found, using empty texture");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to preload urban texture: {ex.Message}");
                    _cachedUrbanTexture = new Image<Rgba32>(1, 1, new Rgba32(0, 0, 0, 0));
                }
            }
        }

        /// <summary>
        /// NEW: Initialize memory pool for byte arrays
        /// </summary>
        private static void InitializeMemoryPool()
        {
            for (int i = 0; i < MaxPooledArrays; i++)
            {
                _byteArrayPool.Enqueue(new byte[PooledArraySize]);
            }
        }

        /// <summary>
        /// NEW: Get a pooled byte array to reduce allocations
        /// </summary>
        private static byte[] GetPooledByteArray(int minSize)
        {
            if (minSize <= PooledArraySize && _byteArrayPool.TryDequeue(out var pooled))
            {
                return pooled;
            }
            return new byte[minSize];
        }

        /// <summary>
        /// NEW: Return a byte array to the pool
        /// </summary>
        private static void ReturnPooledByteArray(byte[] array)
        {
            if (array.Length == PooledArraySize && _byteArrayPool.Count < MaxPooledArrays)
            {
                _byteArrayPool.Enqueue(array);
            }
        }

        // NEW: Accessors for shared GDAL datasets
        private static Dataset GetTerrainDataset()
        {
            lock (_dataSourceLock)
            {
                _terrainDataset ??= Gdal.Open(TerrainTifPath, Access.GA_ReadOnly);
                return _terrainDataset;
            }
        }

        private static DataSource GetCountryDataSource()
        {
            lock (_dataSourceLock)
            {
                _countryDataSource ??= Ogr.Open(ShpPath, 0);
                return _countryDataSource;
            }
        }

        /// <summary>
        /// Ensures the GeoTIFF file exists by invoking fetch_etopo1.py if needed.
        /// </summary>
        public static void EnsureElevationData()
        {
            if (File.Exists(TifPath))
                return;
        }

        /// <summary>
        /// NEW: Get cached country mask or create if not cached
        /// </summary>
        private static int[,] GetCachedCountryMask(int fullWidth, int fullHeight, int offsetX, int offsetY, int width, int height)
        {
            var key = (width, height, offsetX, offsetY);

            if (_countryMaskCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var mask = CreateCountryMaskTile(fullWidth, fullHeight, offsetX, offsetY, width, height);

            // Only cache if the cache isn't too large (prevent memory bloat)
            if (_countryMaskCache.Count < 1000)
            {
                _countryMaskCache.TryAdd(key, mask);
            }

            return mask;
        }

        /// <summary>
        /// NEW: Get cached terrain data or load if not cached (LEGACY - for cell-based access)
        /// </summary>
        private static (byte[] r, byte[] g, byte[] b) GetCachedTerrainDataLegacy(int cellSize, int startCellX, int startCellY, int cellsX, int cellsY, int mapWidth, int mapHeight)
        {
            var key = (startCellX, startCellY, cellsX, cellsY, mapWidth * mapHeight);
            
            if (_terrainDataCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            // Load terrain data
            var terrainData = LoadTerrainData(startCellX, startCellY, cellsX, cellsY, mapWidth, mapHeight);

            // Only cache if the cache isn't too large
            if (_terrainDataCache.Count < 500)
            {
                _terrainDataCache.TryAdd(key, terrainData);
            }

            return terrainData;
        }

        /// <summary>
        /// NEW: Load terrain data from GDAL (separated for caching)
        /// </summary>
        private static (byte[] r, byte[] g, byte[] b) LoadTerrainData(int startCellX, int startCellY, int cellsX, int cellsY, int mapWidth, int mapHeight)
        {
            lock (GdalConfigLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }

            var dsTerrain = GetTerrainDataset();
            if (dsTerrain == null) throw new FileNotFoundException("Missing terrain GeoTIFF", TerrainTifPath);

            int srcW = dsTerrain.RasterXSize;
            int srcH = dsTerrain.RasterYSize;

            double scaleX = (double)srcW / mapWidth;
            double scaleY = (double)srcH / mapHeight;
            int srcX = (int)(startCellX * scaleX);
            int srcY = (int)(startCellY * scaleY);
            int readW = (int)Math.Ceiling(cellsX * scaleX);
            int readH = (int)Math.Ceiling(cellsY * scaleY);

            byte[] r = GetPooledByteArray(cellsX * cellsY);
            byte[] g = GetPooledByteArray(cellsX * cellsY);
            byte[] b = GetPooledByteArray(cellsX * cellsY);

            try
            {
                dsTerrain.GetRasterBand(1).ReadRaster(srcX, srcY, readW, readH, r, cellsX, cellsY, 0, 0);
                dsTerrain.GetRasterBand(2).ReadRaster(srcX, srcY, readW, readH, g, cellsX, cellsY, 0, 0);
                dsTerrain.GetRasterBand(3).ReadRaster(srcX, srcY, readW, readH, b, cellsX, cellsY, 0, 0);

                // Create copies for caching (the pooled arrays will be returned)
                var rCopy = new byte[cellsX * cellsY];
                var gCopy = new byte[cellsX * cellsY];
                var bCopy = new byte[cellsX * cellsY];
                Array.Copy(r, rCopy, cellsX * cellsY);
                Array.Copy(g, gCopy, cellsX * cellsY);
                Array.Copy(b, bCopy, cellsX * cellsY);

                return (rCopy, gCopy, bCopy);
            }
            finally
            {
                ReturnPooledByteArray(r);
                ReturnPooledByteArray(g);
                ReturnPooledByteArray(b);
            }
        }

        /// <summary>
        /// Generate a single tile of the pixel-art map directly from the terrain
        /// raster. Only the required region is read using GDAL.
        /// OPTIMIZED: Now with caching and multithreading support
        /// FIXED: Correct coordinate transformation to prevent tiling artifacts
        /// FIXED: Properly handle actual tile dimensions for edge tiles
        /// </summary>
        private static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>
     GenerateTerrainTileLarge(int mapWidth, int mapHeight, int cellSize, int tileX, int tileY, int tileSizePx = 512, int[,] landMask = null)
        {
            // --- Coordinate and Size Calculations ---
            int mapWidthPx = mapWidth * cellSize;
            int mapHeightPx = mapHeight * cellSize;
            int pixelX = tileX * 512; // Always use standard tile size for offset calculation
            int pixelY = tileY * 512; // Always use standard tile size for offset calculation
            
            // Use the actual tile dimensions (which may be smaller for edge tiles)
            int tileWidth = Math.Min(tileSizePx, mapWidthPx - pixelX);
            int tileHeight = Math.Min(tileSizePx, mapHeightPx - pixelY);
            
            if (tileWidth <= 0 || tileHeight <= 0) return new Image<Rgba32>(1, 1);

            // Use provided land mask or create one with actual dimensions
            if (landMask == null)
            {
                landMask = GetCachedCountryMask(mapWidthPx, mapHeightPx, pixelX, pixelY, tileWidth, tileHeight);
            }

            // --- FIXED: Calculate terrain data requirements in PIXEL space, not cell space ---
            // The terrain raster should be sampled at pixel resolution, not cell resolution
            int startPixelX = pixelX;
            int startPixelY = pixelY;
            
            // Sample terrain data at the actual pixel resolution needed
            var (r, g, b) = GetCachedTerrainDataForPixels(startPixelX, startPixelY, tileWidth, tileHeight, mapWidthPx, mapHeightPx);

            // Get cached urban texture
            Image<Rgba32> urbanTexture;
            lock (_urbanTextureLock)
            {
                urbanTexture = _cachedUrbanTexture ?? new Image<Rgba32>(1, 1, new Rgba32(0, 0, 0, 0));
            }

            // --- Main Rendering Loop with Parallel Processing ---
            var dest = new Image<Rgba32>(tileWidth, tileHeight);
            var waterColor = new Rgba32(135, 206, 250, 255); // LightSkyBlue

            // Process pixels directly without cell-based subdivision
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount
            };

            Parallel.For(0, tileHeight, parallelOptions, y =>
            {
                var localRandom = _threadLocalRandom.Value;
                var row = dest.DangerousGetPixelRowMemory(y).Span;

                for (int x = 0; x < tileWidth; x++)
                {
                    if (landMask[y, x] == 0)
                    {
                        row[x] = waterColor;
                    }
                    else
                    {
                        // Sample terrain color for this specific pixel
                        int idx = y * tileWidth + x;
                        var terrainColor = new Rgba32(r[idx], g[idx], b[idx], 255);
                        var palette = BuildPalette(terrainColor);

                        // --- Sample and Blend Urban Texture ---
                        int globalPixelX = pixelX + x;
                        int globalPixelY = pixelY + y;
                        int urbanTexX = (int)((double)globalPixelX / mapWidthPx * urbanTexture.Width);
                        int urbanTexY = (int)((double)globalPixelY / mapHeightPx * urbanTexture.Height);

                        Rgba32 urbanColor = urbanTexture[
                            Math.Clamp(urbanTexX, 0, urbanTexture.Width - 1),
                            Math.Clamp(urbanTexY, 0, urbanTexture.Height - 1)
                        ];

                        // Blend the dithered terrain color with the urban color
                        var ditheredTerrain = palette[localRandom.Next(palette.Length)];
                        float urbanAlpha = urbanColor.A / 255f;
                        byte finalR = (byte)(ditheredTerrain.R * (1 - urbanAlpha) + urbanColor.R * urbanAlpha);
                        byte finalG = (byte)(ditheredTerrain.G * (1 - urbanAlpha) + urbanColor.G * urbanAlpha);
                        byte finalB = (byte)(ditheredTerrain.B * (1 - urbanAlpha) + urbanColor.B * urbanAlpha);

                        row[x] = new Rgba32(finalR, finalG, finalB);
                    }
                }
            });

            return dest;
        }

        /// <summary>
        /// NEW: Get cached terrain data for pixels (not cells) to fix coordinate issues
        /// </summary>
        private static (byte[] r, byte[] g, byte[] b) GetCachedTerrainDataForPixels(int startPixelX, int startPixelY, int widthPx, int heightPx, int mapWidthPx, int mapHeightPx)
        {
            var key = (startPixelX, startPixelY, widthPx, heightPx, mapWidthPx);
            
            if (_terrainDataCache.TryGetValue(key, out var cached))
            {
                return cached;
            }

            // Load terrain data at pixel resolution
            var terrainData = LoadTerrainDataForPixels(startPixelX, startPixelY, widthPx, heightPx, mapWidthPx, mapHeightPx);

            // Only cache if the cache isn't too large
            if (_terrainDataCache.Count < 500)
            {
                _terrainDataCache.TryAdd(key, terrainData);
            }

            return terrainData;
        }

        /// <summary>
        /// NEW: Load terrain data for pixel coordinates (not cells) to fix scaling issues
        /// </summary>
        private static (byte[] r, byte[] g, byte[] b) LoadTerrainDataForPixels(int startPixelX, int startPixelY, int widthPx, int heightPx, int mapWidthPx, int mapHeightPx)
        {
            lock (GdalConfigLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }

            var dsTerrain = GetTerrainDataset();
            if (dsTerrain == null) throw new FileNotFoundException("Missing terrain GeoTIFF", TerrainTifPath);

            int srcW = dsTerrain.RasterXSize;
            int srcH = dsTerrain.RasterYSize;

            // Calculate what portion of the source raster we need for this pixel region
            double scaleX = (double)srcW / mapWidthPx;  // FIXED: Use pixel dimensions, not cell dimensions
            double scaleY = (double)srcH / mapHeightPx;  // FIXED: Use pixel dimensions, not cell dimensions
            
            int srcX = (int)(startPixelX * scaleX);
            int srcY = (int)(startPixelY * scaleY);
            int readW = (int)Math.Ceiling(widthPx * scaleX);
            int readH = (int)Math.Ceiling(heightPx * scaleY);

            // Clamp to source bounds
            srcX = Math.Max(0, Math.Min(srcX, srcW - 1));
            srcY = Math.Max(0, Math.Min(srcY, srcH - 1));
            readW = Math.Max(1, Math.Min(readW, srcW - srcX));
            readH = Math.Max(1, Math.Min(readH, srcH - srcY));

            byte[] r = GetPooledByteArray(widthPx * heightPx);
            byte[] g = GetPooledByteArray(widthPx * heightPx);
            byte[] b = GetPooledByteArray(widthPx * heightPx);

            try
            {
                // Read the terrain data and resample to exact pixel dimensions needed
                dsTerrain.GetRasterBand(1).ReadRaster(srcX, srcY, readW, readH, r, widthPx, heightPx, 0, 0);
                dsTerrain.GetRasterBand(2).ReadRaster(srcX, srcY, readW, readH, g, widthPx, heightPx, 0, 0);
                dsTerrain.GetRasterBand(3).ReadRaster(srcX, srcY, readW, readH, b, widthPx, heightPx, 0, 0);

                // Create copies for caching (the pooled arrays will be returned)
                var rCopy = new byte[widthPx * heightPx];
                var gCopy = new byte[widthPx * heightPx];
                var bCopy = new byte[widthPx * heightPx];
                Array.Copy(r, rCopy, widthPx * heightPx);
                Array.Copy(g, gCopy, widthPx * heightPx);
                Array.Copy(b, bCopy, widthPx * heightPx);

                return (rCopy, gCopy, bCopy);
            }
            finally
            {
                ReturnPooledByteArray(r);
                ReturnPooledByteArray(g);
                ReturnPooledByteArray(b);
            }
        }

        /// <summary>
        /// Generate a terrain tile and overlay country borders.
        /// FIXED: Improved coordinate handling to prevent tiling artifacts
        /// FIXED: Properly handle partial tiles with correct dimensions
        /// </summary>
        public static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> GenerateTileWithCountriesLarge(
            int mapWidth, int mapHeight, int cellSize, int tileX, int tileY, int tileSizePx = 512)
        {
            int fullW = mapWidth * cellSize;
            int fullH = mapHeight * cellSize;
            int offsetX = tileX * tileSizePx;
            int offsetY = tileY * tileSizePx;

            int actualWidthPx = Math.Min(tileSizePx, fullW - offsetX);
            int actualHeightPx = Math.Min(tileSizePx, fullH - offsetY);

            if (actualWidthPx <= 0 || actualHeightPx <= 0)
                return new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1, 1);

            // *** THIS IS THE KEY CHANGE ***
            // We now call a new method to get the mask slice from a full-world mask.
            int[,] mask = GetCountryMaskTile(fullW, fullH, offsetX, offsetY, actualWidthPx, actualHeightPx);

            var img = GenerateTerrainTileLarge(mapWidth, mapHeight, cellSize, tileX, tileY, tileSizePx, mask);

            // NOTE: The original code had a call to DrawBordersLarge here which was incorrect.
            // Borders are handled by NaturalEarthOverlayGenerator. This is now correct.

            return img;
        }

        /// <summary>
        /// Determine if a tile contains any land pixels.
        /// </summary>
        public static bool TileContainsLand(int mapWidth, int mapHeight, int cellSize, int tileX, int tileY, int tileSizePx = 512)
        {
            lock (GdalConfigLock)
            {
                if (!_gdalConfigured)
                {
                    GdalBase.ConfigureAll();
                    _gdalConfigured = true;
                }
            }

            int mapWidthPx = mapWidth * cellSize;
            int mapHeightPx = mapHeight * cellSize;

            int pixelX = tileX * tileSizePx;
            int pixelY = tileY * tileSizePx;
            int tileWidth = Math.Min(tileSizePx, mapWidthPx - pixelX);
            int tileHeight = Math.Min(tileSizePx, mapHeightPx - pixelY);
            if (tileWidth <= 0 || tileHeight <= 0)
                return false;

            int[,] mask = GetCountryMaskTile(mapWidthPx, mapHeightPx, pixelX, pixelY, tileWidth, tileHeight);

            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    if (mask[y, x] != 0)
                        return true;
                }
            }

            return false;
        }

        internal static int[,] CreateCountryMaskTile(int fullWidth, int fullHeight,
            int offsetX, int offsetY, int width, int height)
        {
            if (fullWidth == 0 || fullHeight == 0 || width <= 0 || height <= 0)
                return new int[Math.Max(1, height), Math.Max(1, width)];
                
            var dem = GetTerrainDataset();
            double[] gt = new double[6];
            dem.GetGeoTransform(gt);
            int srcCols = dem.RasterXSize;
            int srcRows = dem.RasterYSize;

            // FIXED: Create a geotransform that properly maps the tile region to world coordinates
            // Calculate the world extents for this tile
            double worldWidth = gt[1] * srcCols;   // Total world width in geo units
            double worldHeight = Math.Abs(gt[5]) * srcRows; // Total world height in geo units
            
            double pixelSizeX = worldWidth / fullWidth;   // Geo units per pixel
            double pixelSizeY = worldHeight / fullHeight; // Geo units per pixel
            
            // Calculate the geo bounds for this tile
            double tileWorldX = gt[0] + offsetX * pixelSizeX;
            double tileWorldY = gt[3] - offsetY * pixelSizeY; // Note: Y goes down in pixel space, up in geo space

            double[] newGt = new double[6];
            newGt[0] = tileWorldX;        // Top-left X coordinate
            newGt[1] = pixelSizeX;        // Pixel width in geo units
            newGt[2] = 0;                 // Rotation (typically 0)
            newGt[3] = tileWorldY;        // Top-left Y coordinate  
            newGt[4] = 0;                 // Rotation (typically 0)
            newGt[5] = -pixelSizeY;       // Pixel height in geo units (negative because Y decreases)

            OSGeo.GDAL.Driver memDrv = Gdal.GetDriverByName("MEM");
            using var maskDs = memDrv.Create("", width, height, 1, DataType.GDT_Int32, null);
            maskDs.SetGeoTransform(newGt);
            maskDs.SetProjection(dem.GetProjection());

            var ds = GetCountryDataSource();
            Layer layer = ds.GetLayerByIndex(0);

            Gdal.RasterizeLayer(maskDs, 1, new[] { 1 }, layer, IntPtr.Zero, IntPtr.Zero,
                0, null, new[] { "ATTRIBUTE=ISO_N3" }, null, "");

            Band band = maskDs.GetRasterBand(1);
            int[] flat = new int[width * height];
            band.ReadRaster(0, 0, width, height, flat, width, height, 0, 0);

            int[,] result = new int[height, width];
            for (int r = 0; r < height; r++)
            {
                for (int c = 0; c < width; c++)
                {
                    result[r, c] = flat[r * width + c];
                }
            }

            return result;
        }

        /// <summary>
        /// NEW: Get a tile-sized country mask from a full-world mask.
        /// </summary>
        private static int[,] GetCountryMaskTile(int fullWidth, int fullHeight, int offsetX, int offsetY, int tileWidth, int tileHeight)
        {
            var fullMaskKey = (fullWidth, fullHeight);

            // 1. Get or create the full-world mask from the cache
            if (!_fullCountryMaskCache.TryGetValue(fullMaskKey, out var fullMask))
            {
                // It's not in the cache, so we generate it once for the entire map resolution
                var dem = GetTerrainDataset();
                double[] gt = new double[6];
                dem.GetGeoTransform(gt);

                // Create a memory dataset for the ENTIRE map
                using (var maskDs = Gdal.GetDriverByName("MEM").Create("", fullWidth, fullHeight, 1, DataType.GDT_Int32, null))
                {
                    // Scale the original GeoTransform to the new full-map dimensions
                    double[] newGt = (double[])gt.Clone();
                    newGt[1] = gt[1] * dem.RasterXSize / fullWidth;
                    newGt[5] = gt[5] * dem.RasterYSize / fullHeight;
                    maskDs.SetGeoTransform(newGt);
                    maskDs.SetProjection(dem.GetProjection());

                    // Rasterize the entire country shapefile onto our full-map dataset
                    var ds = GetCountryDataSource();
                    var layer = ds.GetLayerByIndex(0);
                    Gdal.RasterizeLayer(maskDs, 1, new[] { 1 }, layer, IntPtr.Zero, IntPtr.Zero,
                        0, null, new[] { "ATTRIBUTE=ISO_N3" }, null, "");

                    // Read the entire generated mask into a C# array
                    var band = maskDs.GetRasterBand(1);
                    int[] flat = new int[fullWidth * fullHeight];
                    band.ReadRaster(0, 0, fullWidth, fullHeight, flat, fullWidth, fullHeight, 0, 0);

                    fullMask = new int[fullHeight, fullWidth];
                    Buffer.BlockCopy(flat, 0, fullMask, 0, flat.Length * sizeof(int));

                    // Cache the full mask for future tile requests at this resolution
                    _fullCountryMaskCache.TryAdd(fullMaskKey, fullMask);
                }
            }

            // 2. Now that we have the full mask, copy the requested tile section from it.
            var tileMask = new int[tileHeight, tileWidth];
            for (int y = 0; y < tileHeight; y++)
            {
                for (int x = 0; x < tileWidth; x++)
                {
                    int sourceY = offsetY + y;
                    int sourceX = offsetX + x;
                    if (sourceY < fullHeight && sourceX < fullWidth && sourceY >= 0 && sourceX >= 0)
                    {
                        tileMask[y, x] = fullMask[sourceY, sourceX];
                    }
                }
            }
            
            return tileMask;
        }

        /// <summary>
        /// Draw borders directly on an ImageSharp image when the map exceeds
        /// System.Drawing limits.
        /// </summary>
        public static SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>
     DrawBordersLarge(SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> baseImage, int[,] landMask)
        {
            if (!System.IO.File.Exists(ShpPath))
                throw new System.IO.FileNotFoundException("Missing shapefile", ShpPath);

            int heightPx = landMask.GetLength(0);
            int widthPx = landMask.GetLength(1);

            var result = baseImage.Clone();

            SixLabors.ImageSharp.PixelFormats.Rgba32 borderColor = new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 0, 255); // black

            var reader = new NetTopologySuite.IO.ShapefileDataReader(
                ShpPath, NetTopologySuite.Geometries.GeometryFactory.Default);

            while (reader.Read())
            {
                var geometry = reader.Geometry;

                if (geometry is NetTopologySuite.Geometries.MultiPolygon multi)
                {
                    for (int i = 0; i < multi.NumGeometries; i++)
                    {
                        var poly = (NetTopologySuite.Geometries.Polygon)multi.GetGeometryN(i);
                        DrawPolygonOutline(result, poly, widthPx, heightPx, borderColor);
                    }
                }
                else if (geometry is NetTopologySuite.Geometries.Polygon poly)
                {
                    DrawPolygonOutline(result, poly, widthPx, heightPx, borderColor);
                }
            }

            return result;
        }
        private static void DrawPolygonOutline(
    SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image,
    NetTopologySuite.Geometries.Polygon polygon,
    int widthPx,
    int heightPx,
    SixLabors.ImageSharp.PixelFormats.Rgba32 color)
        {
            var exterior = polygon.ExteriorRing.Coordinates;
            for (int i = 0; i < exterior.Length - 1; i++)
            {
                var c0 = exterior[i];
                var c1 = exterior[i + 1];

                // FIXED: Ensure coordinates are properly normalized to the tile bounds
                // Convert from lon/lat space (-180 to 180, -90 to 90) to pixel space
                int x0 = (int)Math.Round((c0.X + 180.0) / 360.0 * widthPx);
                int y0 = (int)Math.Round((90.0 - c0.Y) / 180.0 * heightPx);
                int x1 = (int)Math.Round((c1.X + 180.0) / 360.0 * widthPx);
                int y1 = (int)Math.Round((90.0 - c1.Y) / 180.0 * heightPx);

                DrawLine(image, x0, y0, x1, y1, color);
            }
        }
        private static void DrawLine(
    SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32> image,
    int x0, int y0, int x1, int y1,
    SixLabors.ImageSharp.PixelFormats.Rgba32 color)
        {
            int dx = System.Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -System.Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2;

            while (true)
            {
                if (x0 >= 0 && x0 < image.Width && y0 >= 0 && y0 < image.Height)
                {
                    image[x0, y0] = color;
                }

                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }



        // Builds a small palette of colors around the provided base color.  A
        // darker and lighter variant are included to add variety when filling
        // each cell with multiple pixels.

        private static Rgba32[] BuildPalette(Rgba32 baseColor)
        {
            Rgba32 Lerp(Rgba32 a, Rgba32 b, float t)
            {
                t = Math.Clamp(t, 0f, 1f);
                byte r = (byte)(a.R + (b.R - a.R) * t);
                byte g = (byte)(a.G + (b.G - a.G) * t);
                byte bVal = (byte)(a.B + (b.B - a.B) * t);
                byte aVal = (byte)(a.A + (b.A - a.A) * t);
                return new Rgba32(r, g, bVal, aVal);
            }

            return new[]
            {
        Lerp(baseColor, new Rgba32(0, 0, 0, baseColor.A), 0.2f),
        baseColor,
        Lerp(baseColor, new Rgba32(255, 255, 255, baseColor.A), 0.2f)
    };
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

        /// <summary>
        /// NEW: Clear all caches to free memory or force regeneration
        /// </summary>
        public static void ClearCaches()
        {
            _countryMaskCache.Clear();
            _terrainDataCache.Clear();
            _fullCountryMaskCache.Clear();

            lock (_dataSourceLock)
            {
                _terrainDataset?.Dispose();
                _terrainDataset = null;
                _countryDataSource?.Dispose();
                _countryDataSource = null;
            }

            lock (_urbanTextureLock)
            {
                _cachedUrbanTexture?.Dispose();
                _cachedUrbanTexture = null;
            }

            // Clear memory pool
            while (_byteArrayPool.TryDequeue(out _)) { }
            InitializeMemoryPool();

            Console.WriteLine("[DEBUG] PixelMapGenerator caches cleared");
        }

        /// <summary>
        /// NEW: Get cache statistics for monitoring
        /// </summary>
        public static (int countryMasks, int terrainData, int fullCountryMasks, int memoryPoolSize) GetCacheStats()
        {
            return (_countryMaskCache.Count, _terrainDataCache.Count, _fullCountryMaskCache.Count, _byteArrayPool.Count);
        }
    }
}
