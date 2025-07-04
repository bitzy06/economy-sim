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

namespace StrategyGame
{
    public class CityTileManager
    {
        private readonly int _baseWidth;
        private readonly int _baseHeight;
        private readonly Dictionary<(int cellSize, int x, int y), SD.Bitmap> _tileCache = new();
        private readonly Dictionary<(int cellSize, int x, int y), Task<SD.Bitmap>> _inFlight = new();
        private readonly object _cacheLock = new();
        private readonly object _textureLock = new();
        private readonly Dictionary<(int cellSize, int x, int y), SKBitmap> _tileTextures = new();
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

        public CityTileManager(int baseWidth, int baseHeight)
        {
            _baseWidth = baseWidth;
            _baseHeight = baseHeight;
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
            using var ms = new MemoryStream();
            img.SaveAsPng(ms);
            ms.Position = 0;
            return new SD.Bitmap(ms);
        }

        private void UploadTileTexture((int cellSize, int x, int y) key, SD.Bitmap bmp)
        {
            var sk = SkiaBitmapUtil.ToSKBitmap(bmp);
            lock (_textureLock)
            {
                if (_tileTextures.TryGetValue(key, out var old))
                    old.Dispose();
                _tileTextures[key] = sk;
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

            if (size < 1f)
                size = 1f;

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

        public Task<SD.Bitmap> GetTileAsync(float zoom, int tileX, int tileY, CancellationToken token)
        {
            int cellSize = GetCellSize(zoom);
            var key = (cellSize, tileX, tileY);
            lock (_cacheLock)
            {
                if (_inFlight.TryGetValue(key, out var existing))
                    return existing;
                var task = LoadTileInternalAsync(cellSize, tileX, tileY, token);
                _inFlight[key] = task;
                task.ContinueWith(_ =>
                {
                    lock (_cacheLock)
                    {
                        _inFlight.Remove(key);
                    }
                }, TaskScheduler.Default);
                return task;
            }
        }

        private async Task<SD.Bitmap> LoadTileInternalAsync(int cellSize, int tileX, int tileY, CancellationToken token)
        {
            var key = (cellSize, tileX, tileY);
            lock (_cacheLock)
            {
                if (_tileCache.TryGetValue(key, out var cached))
                    return cached;
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
                    var bmp = ImageSharpToBitmap(img);
                    lock (_cacheLock)
                        _tileCache[key] = bmp;
                    return bmp;
                }
                finally
                {
                    fileLock.Release();
                }
            }

            GeoBounds bounds = ComputeTileBounds(cellSize, tileX, tileY);
            using var generated = await ProceduralCityRenderer.RenderCityTileAsync(bounds, cellSize).ConfigureAwait(false);
            var bitmap = ImageSharpToBitmap(generated);
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

            lock (_cacheLock)
                _tileCache[key] = bitmap;
            UploadTileTexture(key, bitmap);
            return bitmap;
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

                    SKBitmap tex = null;
                    lock (_textureLock)
                        _tileTextures.TryGetValue(key, out tex);

                    if (tex != null)
                    {
                        canvas.DrawBitmap(tex, rect);
                    }
                    else
                    {
                        SD.Bitmap tile = null;
                        lock (_cacheLock)
                            _tileCache.TryGetValue(key, out tile);

                        if (tile != null && tile.Width > 0 && tile.Height > 0)
                        {
                            tex = SkiaBitmapUtil.ToSKBitmap(tile);
                            lock (_textureLock) _tileTextures[key] = tex;
                            canvas.DrawBitmap(tex, rect);
                        }
                        else
                        {
                            _ = Task.Run(async () =>
                            {
                                var t = await GetTileAsync(zoom, tx, ty, CancellationToken.None).ConfigureAwait(false);
                                if (t != null) UploadTileTexture(key, t);
                                triggerRefresh?.Invoke();
                            });
                        }
                    }
                }
            }

            var result = new SKBitmap(info);
            surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0);
            return result;
        }

        public void PreloadVisibleTiles(float zoom, SD.Rectangle viewRect)
        {
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

            Parallel.ForEach(coords, coord =>
            {
                throttle.Wait();
                try
                {
                    GetTileAsync(zoom, coord.x, coord.y, CancellationToken.None)
                        .GetAwaiter().GetResult();
                }
                finally
                {
                    throttle.Release();
                }
            });
        }
    }
}
