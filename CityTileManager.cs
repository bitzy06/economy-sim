using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using SD = System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using SkiaSharp;
using economy_sim;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Buffers;

namespace StrategyGame
{
    public class CityTileManager
    {
        private readonly int _baseWidth;
        private readonly int _baseHeight;
        private readonly MultiResolutionMapManager _mapManager;
        private readonly ConcurrentDictionary<(int cellSize, int x, int y), SKBitmap> _tileCache = new();
        private readonly ConcurrentDictionary<(int cellSize, int x, int y), Task<SKBitmap>> _inFlight = new();
        private readonly SemaphoreSlim _preloadLimiter = new(8, 8);
        private SKBitmap? _viewBuffer;
        private SKSurface? _viewSurface;
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

        private sealed class ImagePixelOwner : IDisposable
        {
            public Image<Rgba32> Image { get; }
            public MemoryHandle Handle { get; }

            public ImagePixelOwner(Image<Rgba32> image)
            {
                Image = image;
                Handle = image.Frames.RootFrame.DangerousTryGetSinglePixelMemory(out var memory) ? memory.Pin() : throw new InvalidOperationException("Unable to pin pixel memory.");
            }

            public void Dispose()
            {
                Handle.Dispose();
                Image.Dispose();
            }
        }

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

        private static unsafe SKBitmap ImageSharpToSkia(Image<Rgba32> img)
        {
            var sw = Stopwatch.StartNew();
            var info = new SKImageInfo(img.Width, img.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var owner = new ImagePixelOwner(img);
            var skBitmap = new SKBitmap();

            skBitmap.InstallPixels(
                info,
                (IntPtr)owner.Handle.Pointer,
                img.Width * Unsafe.SizeOf<Rgba32>(),
                (addr, ctx) => ((ImagePixelOwner)ctx!).Dispose(),
                owner);

            PerformanceTracker.Record("CityTileManager-ImageSharpToSkia", sw.Elapsed);
            return skBitmap;
        }

        private static async Task SaveTileAsync(string path, Image<Rgba32> image)
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var fileLock = GetFileLock(path);
            await fileLock.WaitAsync().ConfigureAwait(false);
            try
            {
                await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);
                await image.SaveAsPngAsync(fs).ConfigureAwait(false);
            }
            finally
            {
                fileLock.Release();
                image.Dispose();
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

            if (_tileCache.TryGetValue(key, out var cached))
                return Task.FromResult(cached);

            return _inFlight.GetOrAdd(key, _ => LoadTileInternalAsync(cellSize, tileX, tileY, token));
        }

        private async Task<SKBitmap> LoadTileInternalAsync(int cellSize, int tileX, int tileY, CancellationToken token)
        {
            var totalSw = Stopwatch.StartNew();
            var key = (cellSize, tileX, tileY);

            if (_tileCache.TryGetValue(key, out var cached))
            {
                _inFlight.TryRemove(key, out _);
                PerformanceTracker.Record("CityTileManager-LoadTile-Cached", totalSw.Elapsed);
                return cached;
            }

            string path = GetTilePath(cellSize, tileX, tileY);
            var fileLock = GetFileLock(path);
            await fileLock.WaitAsync(token).ConfigureAwait(false);
            try
            {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
                var img = await Image.LoadAsync<Rgba32>(fs, token).ConfigureAwait(false);
                var fromDisk = ImageSharpToSkia(img);
                _tileCache[key] = fromDisk;
                _inFlight.TryRemove(key, out _);
                PerformanceTracker.Record("CityTileManager-LoadTile-FromDisk", totalSw.Elapsed);
                return fromDisk;
            }
            catch (FileNotFoundException)
            {
                // fall through to generate
            }
            finally
            {
                fileLock.Release();
            }

            GeoBounds bounds = ComputeTileBounds(cellSize, tileX, tileY);
            var genSw = Stopwatch.StartNew();
            var generated = await ProceduralCityRenderer.RenderCityTileAsync(bounds, cellSize).ConfigureAwait(false);
            var skBmp = ImageSharpToSkia(generated);
            PerformanceTracker.Record("CityTileManager-GenerateTile", genSw.Elapsed);

            _tileCache[key] = skBmp;
            _ = Task.Run(() => SaveTileAsync(path, generated));

            _inFlight.TryRemove(key, out _);
            PerformanceTracker.Record("CityTileManager-LoadTile", totalSw.Elapsed);
            return skBmp;
        }

        public SKBitmap AssembleView(float zoom, SD.Rectangle viewArea, Action triggerRefresh = null)
        {
            var sw = Stopwatch.StartNew();
            int cellSize = GetCellSize(zoom);
            int tileSize = MultiResolutionMapManager.TileSizePx;

            var info = new SKImageInfo(viewArea.Width, viewArea.Height);

            if (_viewBuffer == null || _viewBuffer.Width != info.Width || _viewBuffer.Height != info.Height)
            {
                _viewSurface?.Dispose();
                _viewBuffer?.Dispose();
                _viewBuffer = new SKBitmap(info);
                _viewSurface = SKSurface.Create(info, _viewBuffer.GetPixels(), _viewBuffer.RowBytes);
            }

            var canvas = _viewSurface!.Canvas;
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

                    SKBitmap tileBitmap = null;
                    if (_tileCache.TryGetValue(key, out var cachedTile))
                    {
                        tileBitmap = cachedTile;
                    }

                    if (tileBitmap != null)
                    {
                        canvas.DrawBitmap(tileBitmap, rect);
                    }
                    else
                    {
                        _ = GetTileAsync(zoom, tx, ty, CancellationToken.None);
                    }
                }
            }

            _viewSurface!.Canvas.Flush();
            PerformanceTracker.Record("CityTileManager-AssembleView", sw.Elapsed);
            return _viewBuffer!;
        }

        public async Task PreloadVisibleTilesAsync(float zoom, SD.Rectangle viewRect, int radius = 1, Action triggerRefresh = null, CancellationToken token = default)
        {
            var sw = Stopwatch.StartNew();
            int cellSize = GetCellSize(zoom);
            int tileSize = MultiResolutionMapManager.TileSizePx;
            var mapSize = new SD.Size(_baseWidth * cellSize, _baseHeight * cellSize);

            int startX = Math.Max(0, viewRect.X / tileSize - radius);
            int endX = Math.Min((mapSize.Width - 1) / tileSize, (viewRect.Right - 1) / tileSize + radius);
            int startY = Math.Max(0, viewRect.Y / tileSize - radius);
            int endY = Math.Min((mapSize.Height - 1) / tileSize, (viewRect.Bottom - 1) / tileSize + radius);

            var missingTiles = new List<(int x, int y)>();
            for (int x = startX; x <= endX; x++)
            {
                for (int y = startY; y <= endY; y++)
                {
                    var key = (cellSize, x, y);
                    if (!_tileCache.ContainsKey(key) && !_inFlight.ContainsKey(key))
                    {
                        missingTiles.Add((x, y));
                    }
                }
            }

            if (!missingTiles.Any())
            {
                PerformanceTracker.Record("CityTileManager-PreloadTiles", sw.Elapsed);
                triggerRefresh?.Invoke();
                return;
            }

            var tasks = missingTiles.Select(async coord =>
            {
                await _preloadLimiter.WaitAsync(token).ConfigureAwait(false);
                try { await GetTileAsync(zoom, coord.x, coord.y, token).ConfigureAwait(false); }
                finally { _preloadLimiter.Release(); }
            });
            await Task.WhenAll(tasks).ConfigureAwait(false);
            PerformanceTracker.Record("CityTileManager-PreloadTiles", sw.Elapsed);
            triggerRefresh?.Invoke();
        }
    }
    }
