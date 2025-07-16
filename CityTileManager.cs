using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using SD = System.Drawing;
using SDI = System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using SkiaSharp;
using economy_sim;

namespace StrategyGame
{
    public class CityTileManager
    {
        private readonly int _baseWidth;
        private readonly int _baseHeight;
        // --- START: Added Field ---
        private readonly MultiResolutionMapManager _mapManager;
        // --- END: Added Field ---
        private readonly Dictionary<(int cellSize, int x, int y), (SKBitmap sk, SD.Bitmap gdi)> _tileCache = new();
        private readonly Dictionary<(int cellSize, int x, int y), Task<SKBitmap>> _inFlight = new();
        private readonly object _cacheLock = new();
        private readonly LinkedList<(int cellSize, int x, int y)> _lruOrder = new();
        private readonly Dictionary<(int cellSize, int x, int y), LinkedListNode<(int cellSize, int x, int y)>> _lruNodes = new();
        private const int MaxCacheSize = 256;
        public static readonly bool GpuAvailable;

        static CityTileManager()
        {
            try
            {
                GpuAvailable = MultiResolutionMapManager.SharedContext != null;
            }
            catch
            {
                GpuAvailable = false;
            }
        }
        private static readonly object _fileLockDictLock = new();
        private static readonly Dictionary<string, SemaphoreSlim> _fileLocks = new();

        private static readonly string TileCacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data", "city_tile_cache");

        public CityTileManager(int baseWidth, int baseHeight, MultiResolutionMapManager mapManager)
        {
            _baseWidth = baseWidth;
            _baseHeight = baseHeight;
            // --- START: Added Initialization ---
            _mapManager = mapManager;
            // --- END: Added Initialization ---
        }

        private static SemaphoreSlim GetFileLock(string path)
        {
            lock (_fileLockDictLock)
            {
                if (!_fileLocks.TryGetValue(path, out var sem))
                {
                    sem = new SemaphoreSlim(1, 1);
                    _fileLocks[path] = sem;
                }
                return sem;
            }
        }

        private static SD.Bitmap ImageSharpToBitmap(Image<Rgba32> img)
        {
            var sw = Stopwatch.StartNew();
            using var ms = new MemoryStream();
            img.SaveAsPng(ms);
            ms.Position = 0;
            var bmp = new SD.Bitmap(ms);
            PerformanceTracker.Record("ImageSharpToBitmap", sw.Elapsed);
            return bmp;
        }


        private void TouchKey((int cellSize, int x, int y) key)
        {
            var sw = Stopwatch.StartNew();
            lock (_cacheLock)
            {
                if (_lruNodes.TryGetValue(key, out var node))
                {
                    _lruOrder.Remove(node);
                    _lruOrder.AddFirst(node);
                }
            }
            PerformanceTracker.Record("LRU-TouchKey", sw.Elapsed);
        }

        private void AddToCache((int cellSize, int x, int y) key, (SKBitmap sk, SD.Bitmap gdi) bitmaps)
        {
            var sw = Stopwatch.StartNew();
            lock (_cacheLock)
            {
                _tileCache[key] = bitmaps;
                if (_lruNodes.TryGetValue(key, out var existing))
                {
                    _lruOrder.Remove(existing);
                }
                var node = _lruOrder.AddFirst(key);
                _lruNodes[key] = node;

                while (_tileCache.Count > MaxCacheSize)
                {
                    var last = _lruOrder.Last;
                    if (last == null) break;
                    _lruOrder.RemoveLast();
                    var remKey = last.Value;
                    if (_tileCache.TryGetValue(remKey, out var oldBitmaps))
                    {
                        oldBitmaps.sk.Dispose();
                        oldBitmaps.gdi.Dispose();
                    }
                    _tileCache.Remove(remKey);
                    _lruNodes.Remove(remKey);
                }
            }
            PerformanceTracker.Record("AddToCache", sw.Elapsed);
        }

        private int GetCellSize(float zoom)
        {
            var sw = Stopwatch.StartNew();
            float[] anchors = new float[MultiResolutionMapManager.PixelsPerCellLevels.Length];
            for (int i = 0; i < anchors.Length; i++)
                anchors[i] = MultiResolutionMapManager.PixelsPerCellLevels[i];

            float size;
            if (zoom <= 1f)
                size = anchors[0];
            else if (zoom >= anchors.Length)
                size = anchors[^1];
            else
            {
                int lower = (int)Math.Floor(zoom) - 1;
                float t = zoom - (lower + 1);
                size = anchors[lower] + t * (anchors[lower + 1] - anchors[lower]);
            }

            if (size < 1f)
                size = 1f;

            var result = (int)Math.Round(size);
            PerformanceTracker.Record("GetCellSize", sw.Elapsed);
            return result;
        }

        private GeoBounds ComputeTileBounds(int cellSize, int tileX, int tileY)
        {
            var sw = Stopwatch.StartNew();
            int fullW = _baseWidth * cellSize;
            int fullH = _baseHeight * cellSize;
            int offsetX = tileX * MultiResolutionMapManager.TileSizePx;
            int offsetY = tileY * MultiResolutionMapManager.TileSizePx;
            int tileWidth = Math.Min(MultiResolutionMapManager.TileSizePx, fullW - offsetX);
            int tileHeight = Math.Min(MultiResolutionMapManager.TileSizePx, fullH - offsetY);

            var bounds = new GeoBounds
            {
                MinLon = -180 + (double)offsetX / fullW * 360.0,
                MaxLon = -180 + (double)(offsetX + tileWidth) / fullW * 360.0,
                MaxLat = 90 - (double)offsetY / fullH * 180.0,
                MinLat = 90 - (double)(offsetY + tileHeight) / fullH * 180.0
            };
            PerformanceTracker.Record("ComputeTileBounds", sw.Elapsed);
            return bounds;
        }

        private string GetTilePath(int cellSize, int tileX, int tileY)
        {
            string tileFolder = Path.Combine(TileCacheDir, cellSize.ToString());
            return Path.Combine(tileFolder, $"{tileX}_{tileY}.png");
        }

        public Task<SKBitmap> GetTileAsync(float zoom, int tileX, int tileY, CancellationToken token)
        {
            var sw = Stopwatch.StartNew();
            int cellSize = GetCellSize(zoom);
            var key = (cellSize, tileX, tileY);
            Task<SKBitmap> result;
            lock (_cacheLock)
            {
                if (_inFlight.TryGetValue(key, out var existing))
                {
                    result = existing;
                }
                else
                {
                    var task = LoadTileInternalAsync(cellSize, tileX, tileY, token);
                    _inFlight[key] = task;
                    task.ContinueWith(_ =>
                    {
                        lock (_cacheLock)
                        {
                            _inFlight.Remove(key);
                        }
                    }, TaskScheduler.Default);
                    result = task;
                }
            }
            PerformanceTracker.Record("GetTileAsync", sw.Elapsed);
            return result;
        }

        private async Task<SKBitmap> LoadTileInternalAsync(int cellSize, int tileX, int tileY, CancellationToken token)
        {
            var sw = Stopwatch.StartNew();
            var key = (cellSize, tileX, tileY);
            lock (_cacheLock)
            {
                if (_tileCache.TryGetValue(key, out var cached))
                {
                    TouchKey(key);
                    PerformanceTracker.Record("LoadTileInternal-CacheHit", sw.Elapsed);
                    return cached.sk;
                }
            }

            string path = GetTilePath(cellSize, tileX, tileY);
            if (File.Exists(path))
            {
                var swFileLoad = Stopwatch.StartNew();
                var fileLock = GetFileLock(path);
                await fileLock.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                    using var img = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(fs, token).ConfigureAwait(false);
                    var bmp = ImageSharpToBitmap(img);
                    var sk = SkiaBitmapUtil.ToSKBitmap(bmp);
                    AddToCache(key, (sk, bmp));
                    PerformanceTracker.Record("LoadTileInternal-FromDisk", swFileLoad.Elapsed);
                    PerformanceTracker.Record("LoadTileInternal-Total", sw.Elapsed);
                    return sk;
                }
                finally
                {
                    fileLock.Release();
                }
            }

            var swGeneration = Stopwatch.StartNew();
            GeoBounds bounds = ComputeTileBounds(cellSize, tileX, tileY);
            var swGen = Stopwatch.StartNew();
            using var generated = await ProceduralCityRenderer.RenderCityTileAsync(bounds, cellSize).ConfigureAwait(false);
            PerformanceTracker.Record("TileGeneration", swGen.Elapsed);
            
            var swBitmap = Stopwatch.StartNew();
            var bitmap = ImageSharpToBitmap(generated);
            PerformanceTracker.Record("TileGeneration-ToBitmap", swBitmap.Elapsed);
            
            var swSave = Stopwatch.StartNew();
            string dir = Path.Combine(TileCacheDir, cellSize.ToString());
            Directory.CreateDirectory(dir);
            var lockFile = GetFileLock(path);
            await lockFile.WaitAsync(token).ConfigureAwait(false);
            try
            {
                using var clone = generated.Clone();
                await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await clone.SaveAsPngAsync(fs, token).ConfigureAwait(false);
            }
            finally
            {
                lockFile.Release();
            }
            PerformanceTracker.Record("TileGeneration-SaveToDisk", swSave.Elapsed);

            var skBmp = SkiaBitmapUtil.ToSKBitmap(bitmap);
            AddToCache(key, (skBmp, bitmap));
            PerformanceTracker.Record("LoadTileInternal-Generation", swGeneration.Elapsed);
            PerformanceTracker.Record("LoadTileInternal-Total", sw.Elapsed);
            return skBmp;
        }

        public SKBitmap AssembleView(float zoom, SD.Rectangle viewArea, Action triggerRefresh = null)
        {
            var swRender = Stopwatch.StartNew();
            int cellSize = GetCellSize(zoom);
            int tileSize = MultiResolutionMapManager.TileSizePx;
            
            var swSurface = Stopwatch.StartNew();
            var info = new SKImageInfo(viewArea.Width, viewArea.Height);
            var context = GpuAvailable ? MultiResolutionMapManager.SharedContext : null;
            using var surface = context != null ? SKSurface.Create(context, false, info) : SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            PerformanceTracker.Record("AssembleView-CreateSurface", swSurface.Elapsed);

            // --- START: Layer Compositing Fix ---
            // Render the base map layer before drawing city tiles.
            using (var baseMap = _mapManager.AssembleView(zoom, viewArea, triggerRefresh))
            {
                if (baseMap != null)
                {
                    canvas.DrawBitmap(baseMap, SKRect.Create(0, 0, viewArea.Width, viewArea.Height));
                }
            }
            // --- END: Layer Compositing Fix ---

            int tileStartX = Math.Max(0, viewArea.X / tileSize);
            int tileStartY = Math.Max(0, viewArea.Y / tileSize);
            int tileEndX = (viewArea.Right + tileSize - 1) / tileSize;
            int tileEndY = (viewArea.Bottom + tileSize - 1) / tileSize;

            var swDrawing = Stopwatch.StartNew();
            int tilesDrawn = 0;
            int tilesMissing = 0;
            
            for (int ty = tileStartY; ty < tileEndY; ty++)
            {
                for (int tx = tileStartX; tx < tileEndX; tx++)
                {
                    var key = (cellSize, tx, ty);
                    var rect = new SKRect(
                        tx * tileSize - viewArea.X,
                        ty * tileSize - viewArea.Y,
                        tx * tileSize - viewArea.X + tileSize,
                        ty * tileSize - viewArea.Y + tileSize);

                    // --- START: Replacement Logic ---
                    SKBitmap tileCopy = null;
                    lock (_cacheLock)
                    {
                        if (_tileCache.TryGetValue(key, out var cachedTile))
                        {
                            TouchKey(key);
                            // Create a private, safe copy of the tile inside the lock
                            tileCopy = cachedTile.sk.Copy();
                        }
                    }

                    if (tileCopy != null)
                    {
                        using (tileCopy)
                        {
                            try
                            {
                                canvas.DrawBitmap(tileCopy, rect);
                                tilesDrawn++;
                            }
                            catch (AccessViolationException ex)
                            {
                                // This catch block is now just a fallback
                                DebugLogger.Log($"Access violation drawing tile copy {key}: {ex.Message}");
                            }
                        }
                    }
                    else
                    {
                        tilesMissing++;
                        var ttx = tx;
                        var tty = ty;
                        var tileKey = key;
                        _ = Task.Run(async () =>
                        {
                            var swAsync = Stopwatch.StartNew();
                            var t = await GetTileAsync(zoom, ttx, tty, CancellationToken.None).ConfigureAwait(false);
                            if (t != null) triggerRefresh?.Invoke();
                            PerformanceTracker.Record("AssembleView-AsyncTileLoad", swAsync.Elapsed);
                        });
                    }
                    // --- END: Replacement Logic ---
                }
            }
            PerformanceTracker.Record("AssembleView-DrawingTiles", swDrawing.Elapsed);
            PerformanceTracker.Record("AssembleView-TilesDrawn", TimeSpan.FromMilliseconds(tilesDrawn));
            PerformanceTracker.Record("AssembleView-TilesMissing", TimeSpan.FromMilliseconds(tilesMissing));

            var swReadPixels = Stopwatch.StartNew();
            var result = new SKBitmap(info);
            surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0);
            PerformanceTracker.Record("AssembleView-ReadPixels", swReadPixels.Elapsed);
            PerformanceTracker.Record("TileRendering", swRender.Elapsed);
            return result;
        }

        public async Task PreloadVisibleTilesAsync(float zoom, SD.Rectangle viewRect, CancellationToken token = default)
        {
            var sw = Stopwatch.StartNew();
            int cellSize = GetCellSize(zoom);
            int tileSize = MultiResolutionMapManager.TileSizePx;
            int startX = Math.Max(0, viewRect.X / tileSize);
            int endX = (viewRect.Right + tileSize - 1) / tileSize;
            int startY = Math.Max(0, viewRect.Y / tileSize);
            int endY = (viewRect.Bottom + tileSize - 1) / tileSize;

            var coords = Enumerable
                .Range(startX, endX - startX)
                .SelectMany(x => Enumerable.Range(startY, endY - startY)
                    .Select(y => (x, y)))
                .ToList();

            using var throttle = new SemaphoreSlim(Environment.ProcessorCount);
            var tasks = new List<Task>();

            foreach (var coord in coords)
            {
                await throttle.WaitAsync(token).ConfigureAwait(false);
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await GetTileAsync(zoom, coord.x, coord.y, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        throttle.Release();
                    }
                }, token));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);

            PerformanceTracker.Record("PreloadTiles-Total", sw.Elapsed);
        }
    }
}
