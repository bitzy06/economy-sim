using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using SD = System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using SkiaSharp;
using economy_sim;
using SixLabors.ImageSharp.Advanced;
using System.Runtime.InteropServices;

namespace StrategyGame
{
    public class CityTileManager
    {
        private readonly int _baseWidth;
        private readonly int _baseHeight;
        private readonly MultiResolutionMapManager _mapManager;
        private readonly Dictionary<(int cellSize, int x, int y), SKBitmap> _tileCache = new();
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
            _mapManager = mapManager;
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

        private static SKBitmap ImageSharpToSkia(Image<Rgba32> img)
        {
            var info = new SKImageInfo(img.Width, img.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var skBitmap = new SKBitmap(info);

            // Use a high-performance method to get a memory span of the whole image
            if (img.TryGetSinglePixelSpan(out Span<Rgba32> pixelSpan))
            {
                // Safely reinterpret the Rgba32 span as a byte span without allocation
                var byteSpan = MemoryMarshal.AsBytes(pixelSpan);

                // Copy the raw byte data directly to the Skia bitmap's memory
                byteSpan.CopyTo(new Span<byte>((void*)skBitmap.GetPixels(), byteSpan.Length));
            }
            else
            {
                // Provide a fallback for rare cases where image memory isn't contiguous
                img.ProcessPixelRows(accessor =>
                {
                    var ptr = skBitmap.GetPixels();
                    for (int y = 0; y < accessor.Height; y++)
                    {
                        var byteSpan = MemoryMarshal.AsBytes(accessor.GetRowSpan(y));
                        byteSpan.CopyTo(new Span<byte>((void*)(ptr + y * skBitmap.RowBytes), byteSpan.Length));
                    }
                });
            }
            return skBitmap;
        }

        private void TouchKey((int cellSize, int x, int y) key)
        {
            lock (_cacheLock)
            {
                if (_lruNodes.TryGetValue(key, out var node))
                {
                    _lruOrder.Remove(node);
                    _lruOrder.AddFirst(node);
                }
            }
        }

        private void AddToCache((int cellSize, int x, int y) key, SKBitmap bitmap)
        {
            lock (_cacheLock)
            {
                if (_tileCache.ContainsKey(key))
                {
                    _tileCache[key]?.Dispose();
                }

                _tileCache[key] = bitmap;
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

                    var remKey = last.Value;
                    if (_tileCache.TryGetValue(remKey, out var oldBitmap))
                    {
                        oldBitmap?.Dispose();
                    }
                    _tileCache.Remove(remKey);
                    _lruNodes.Remove(remKey);
                    _lruOrder.RemoveLast();
                }
            }
        }

        private int GetCellSize(float zoom)
        {
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

            if (size < 1f) size = 1f;
            return (int)Math.Round(size);
        }

        private GeoBounds ComputeTileBounds(int cellSize, int tileX, int tileY)
        {
            int fullW = _baseWidth * cellSize;
            int fullH = _baseHeight * cellSize;
            int offsetX = tileX * MultiResolutionMapManager.TileSizePx;
            int offsetY = tileY * MultiResolutionMapManager.TileSizePx;
            int tileWidth = Math.Min(MultiResolutionMapManager.TileSizePx, fullW - offsetX);
            int tileHeight = Math.Min(MultiResolutionMapManager.TileSizePx, fullH - offsetY);

            return new GeoBounds
            {
                MinLon = -180 + (double)offsetX / fullW * 360.0,
                MaxLon = -180 + (double)(offsetX + tileWidth) / fullW * 360.0,
                MaxLat = 90 - (double)offsetY / fullH * 180.0,
                MinLat = 90 - (double)(offsetY + tileHeight) / fullH * 180.0
            };
        }

        private string GetTilePath(int cellSize, int tileX, int tileY)
        {
            string tileFolder = Path.Combine(TileCacheDir, cellSize.ToString());
            return Path.Combine(tileFolder, $"{tileX}_{tileY}.png");
        }

        public Task<SKBitmap> GetTileAsync(float zoom, int tileX, int tileY, CancellationToken token)
        {
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
            return result;
        }

        private async Task<SKBitmap> LoadTileInternalAsync(int cellSize, int tileX, int tileY, CancellationToken token)
        {
            var key = (cellSize, tileX, tileY);
            lock (_cacheLock)
            {
                if (_tileCache.TryGetValue(key, out var cached))
                {
                    TouchKey(key);
                    return cached;
                }
            }

            string path = GetTilePath(cellSize, tileX, tileY);
            if (File.Exists(path))
            {
                var fileLock = GetFileLock(path);
                await fileLock.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                    using var img = await SixLabors.ImageSharp.Image.LoadAsync<Rgba32>(fs, token).ConfigureAwait(false);
                    var sk = ImageSharpToSkia(img);
                    AddToCache(key, sk);
                    return sk;
                }
                finally
                {
                    fileLock.Release();
                }
            }

            GeoBounds bounds = ComputeTileBounds(cellSize, tileX, tileY);
            using var generated = await ProceduralCityRenderer.RenderCityTileAsync(bounds, cellSize).ConfigureAwait(false);
            var skBmp = ImageSharpToSkia(generated);

            string dir = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dir);
            var lockFile = GetFileLock(path);
            await lockFile.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await generated.SaveAsPngAsync(fs, token).ConfigureAwait(false);
            }
            finally
            {
                lockFile.Release();
            }

            AddToCache(key, skBmp);
            return skBmp;
        }

        public SKBitmap AssembleView(float zoom, SD.Rectangle viewArea, Action triggerRefresh = null)
        {
            int cellSize = GetCellSize(zoom);
            int tileSize = MultiResolutionMapManager.TileSizePx;

            var info = new SKImageInfo(viewArea.Width, viewArea.Height);
            var context = GpuAvailable ? MultiResolutionMapManager.SharedContext : null;
            using var surface = context != null ? SKSurface.Create(context, false, info) : SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            using (var baseMap = _mapManager.AssembleView(zoom, viewArea, triggerRefresh))
            {
                if (baseMap != null)
                {
                    canvas.DrawBitmap(baseMap, SKRect.Create(0, 0, viewArea.Width, viewArea.Height));
                }
            }

            int tileStartX = Math.Max(0, viewArea.X / tileSize);
            int tileStartY = Math.Max(0, viewArea.Y / tileSize);
            int tileEndX = (viewArea.Right + tileSize - 1) / tileSize;
            int tileEndY = (viewArea.Bottom + tileSize - 1) / tileSize;

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

                    SKBitmap tileCopy = null;
                    lock (_cacheLock)
                    {
                        if (_tileCache.TryGetValue(key, out var cachedTile))
                        {
                            TouchKey(key);
                            tileCopy = cachedTile.Copy();
                        }
                    }

                    if (tileCopy != null)
                    {
                        using (tileCopy)
                        {
                            canvas.DrawBitmap(tileCopy, rect);
                        }
                    }
                    else
                    {
                        _ = GetTileAsync(zoom, tx, ty, CancellationToken.None);
                    }
                }
            }

            var result = new SKBitmap(info);
            surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0);
            return result;
        }

        public async Task PreloadVisibleTilesAsync(float zoom, SD.Rectangle viewRect, int radius = 1, Action triggerRefresh = null, CancellationToken token = default)
        {
            int cellSize = GetCellSize(zoom);
            int tileSize = MultiResolutionMapManager.TileSizePx;

            var mapSize = new SD.Size(_baseWidth * cellSize, _baseHeight * cellSize);

            int startX = Math.Max(0, viewRect.X / tileSize - radius);
            int endX = Math.Min((mapSize.Width - 1) / tileSize, (viewRect.Right - 1) / tileSize + radius);
            int startY = Math.Max(0, viewRect.Y / tileSize - radius);
            int endY = Math.Min((mapSize.Height - 1) / tileSize, (viewRect.Bottom - 1) / tileSize + radius);

            var coords = new List<(int x, int y)>();
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    coords.Add((x, y));
                }
            }

            using var throttle = new SemaphoreSlim(Environment.ProcessorCount);
            var tasks = new List<Task>();

            foreach (var coord in coords)
            {
                var key = (cellSize, coord.x, coord.y);
                lock(_cacheLock)
                {
                    if(_tileCache.ContainsKey(key)) continue;
                }

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

            triggerRefresh?.Invoke();
        }
    }
}
