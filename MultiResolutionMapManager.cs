using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Buffers;
using System.Runtime.CompilerServices;
using SkiaSharp;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace StrategyGame
{
    public class MultiResolutionMapManager
    {
        private readonly HashSet<(int cellSize, int tileX, int tileY)> _tilesBeingLoaded = new();
        private readonly object _tileLoadLock = new();
        private readonly Dictionary<(int cellSize, int tileX, int tileY), Task<SKBitmap>> _inFlightTasks = new();
        private readonly object _taskLock = new();
        private readonly Dictionary<(int cellSize, int x, int y), SKBitmap> _tileTextures = new();
        private readonly LinkedList<(int cellSize, int x, int y)> _tileLru = new();
        private readonly object _masterCacheLock = new();
        public static readonly bool GpuAvailable;
        internal static readonly GRContext? SharedContext;
        static MultiResolutionMapManager()
        {
            try { SharedContext = GRContext.CreateGl(); GpuAvailable = SharedContext != null; } catch { GpuAvailable = false; }
        }
        private const int TileCacheLimit = 1024;
        public const int TileSizePx = 512;
        public static readonly int[] PixelsPerCellLevels = { 3, 4, 6, 10, 40, 80, 160, 320, 640, 1280 };
        private static readonly Dictionary<string, SemaphoreSlim> _fileLocks = new();
        private static readonly object _fileLockDictLock = new();
        private static readonly string TileCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "data", "tile_cache");
        private readonly int _baseWidth;
        private readonly int _baseHeight;

        public MultiResolutionMapManager(int baseWidth, int baseHeight)
        {
            _baseWidth = baseWidth;
            _baseHeight = baseHeight;
        }

        // This method is now identical to your working version. It expects viewArea in pixels.
        public SKBitmap AssembleView(float zoom, SKRectI viewArea, Action triggerRefresh = null)
        {
            int cellSize = GetCellSize(zoom);
            int tileSize = TileSizePx;

            if (viewArea.Width <= 0 || viewArea.Height <= 0 || cellSize <= 0)
                return new SKBitmap(1, 1);

            int tileStartX = Math.Max(0, viewArea.Left / tileSize);
            int tileStartY = Math.Max(0, viewArea.Top / tileSize);
            int tileEndX = (viewArea.Right + tileSize - 1) / tileSize;
            int tileEndY = (viewArea.Bottom + tileSize - 1) / tileSize;

            // Create a bitmap of the exact size requested. No scaling is done here.
            var info = new SKImageInfo(viewArea.Width, viewArea.Height);
            using var surface = GpuAvailable ? SKSurface.Create(SharedContext, false, info) : SKSurface.Create(info);
            var canvas = surface.Canvas;
            canvas.Clear(SKColors.Transparent);

            for (int ty = tileStartY; ty < tileEndY; ty++)
            {
                for (int tx = tileStartX; tx < tileEndX; tx++)
                {
                    var key = (cellSize, tx, ty);
                    // This rect calculates where to draw the tile inside the destination bitmap.
                    var rect = new SKRect(
                        tx * tileSize - viewArea.Left,
                        ty * tileSize - viewArea.Top,
                        tx * tileSize - viewArea.Left + tileSize,
                        ty * tileSize - viewArea.Top + tileSize
                    );

                    if (rect.Width <= 0 || rect.Height <= 0) continue;

                    SKBitmap textureCopy = null;
                    lock (_masterCacheLock)
                    {
                        if (_tileTextures.TryGetValue(key, out var texture))
                        {
                            textureCopy = texture.Copy();
                        }
                    }

                    if (textureCopy != null)
                    {
                        using (textureCopy) { canvas.DrawBitmap(textureCopy, rect); }
                    }
                    else
                    {
                        QueueTileLoad(zoom, tx, ty, triggerRefresh);
                    }
                }
            }

            var result = new SKBitmap(info);
            surface.ReadPixels(result.Info, result.GetPixels(), result.RowBytes, 0, 0);
            return result;
        }

        // This public version is needed by GameView
        public int GetCellSize(float zoom)
        {
            float[] anchors = Array.ConvertAll(PixelsPerCellLevels, x => (float)x);
            float size;
            if (zoom <= 1f)
                size = anchors[0];
            else if (zoom >= anchors.Length)
                size = anchors[^1];
            else
            {
                int lower = (int)Math.Floor(zoom) - 1;
                float t = zoom - (lower + 1);
                if (lower < 0) lower = 0;
                if (lower >= anchors.Length - 1) return (int)Math.Round(anchors[^1]);
                size = anchors[lower] + t * (anchors[lower + 1] - anchors[lower]);
            }
            return (int)Math.Max(1f, Math.Round(size));
        }

        private void QueueTileLoad(float zoom, int tx, int ty, Action triggerRefresh)
        {
            var key = (GetCellSize(zoom), tx, ty);
            lock (_tileLoadLock)
                if (!_tilesBeingLoaded.Add(key)) return;
            _ = Task.Run(async () =>
            {
                try { await GetTileAsync(zoom, tx, ty, CancellationToken.None); }
                catch (Exception ex) { Debug.WriteLine(ex); }
                finally
                {
                    lock (_tileLoadLock) _tilesBeingLoaded.Remove(key);
                    triggerRefresh?.Invoke();
                }
            });
        }

        public Task<SKBitmap> GetTileAsync(float zoom, int tileX, int tileY, CancellationToken token)
        {
            int cellSize = GetCellSize(zoom);
            var key = (cellSize, tileX, tileY);
            lock (_taskLock)
            {
                if (_inFlightTasks.TryGetValue(key, out var existing)) return existing;
                var task = LoadTileInternalAsync(zoom, tileX, tileY, token);
                _inFlightTasks[key] = task;
                task.ContinueWith(_ => { lock (_taskLock) { _inFlightTasks.Remove(key); } }, TaskScheduler.Default);
                return task;
            }
        }

        private async Task<SKBitmap> LoadTileInternalAsync(float zoom, int tileX, int tileY, CancellationToken token)
        {
            int cellSize = GetCellSize(zoom);
            var key = (cellSize, tileX, tileY);
            SKBitmap bmp = null;
            lock (_masterCacheLock)
            {
                if (_tileTextures.TryGetValue(key, out var cached))
                {
                    _tileLru.Remove(key);
                    _tileLru.AddLast(key);
                    return cached;
                }
            }
            string path = Path.Combine(TileCacheDir, cellSize.ToString(), $"{tileX}_{tileY}.png");
            if (File.Exists(path))
            {
                try
                {
                    var fileLock = GetFileLock(path);
                    await fileLock.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        using var img = Image.Load<Rgba32>(stream);
                        if (img.Width > 0 && img.Height > 0) bmp = ImageSharpToSkBitmap(img);
                    }
                    finally { fileLock.Release(); }
                }
                catch (Exception ex) { Debug.WriteLine($"Failed to load tile {tileX},{tileY}: {ex.Message}"); }
            }
            if (bmp == null)
            {
                try
                {
                    bmp = await LoadOrGenerateTileFromDataAsync(cellSize, tileX, tileY, token).ConfigureAwait(false);
                    if (bmp != null && bmp.Width > 0 && bmp.Height > 0)
                    {
                        await SaveTileToDiskAsync(cellSize, tileX, tileY, bmp, token).ConfigureAwait(false);
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"Tile generation failed ({tileX},{tileY}): {ex.Message}"); }
            }
            if (bmp != null)
            {
                lock (_masterCacheLock)
                {
                    if (_tileTextures.ContainsKey(key)) _tileTextures[key]?.Dispose();
                    _tileTextures[key] = bmp;
                    _tileLru.Remove(key);
                    _tileLru.AddLast(key);
                    EnforceTileLimit();
                }
            }
            return bmp;
        }

        private void EnforceTileLimit()
        {
            while (_tileLru.Count > TileCacheLimit)
            {
                var oldest = _tileLru.First.Value;
                _tileLru.RemoveFirst();
                if (_tileTextures.Remove(oldest, out var bitmapToDispose))
                {
                    bitmapToDispose?.Dispose();
                }
            }
        }
        private static SemaphoreSlim GetFileLock(string path)
        {
            lock (_fileLockDictLock)
            {
                if (!_fileLocks.TryGetValue(path, out var sem)) { sem = new SemaphoreSlim(1, 1); _fileLocks[path] = sem; }
                return sem;
            }
        }
        private sealed class ImagePixelOwner : IDisposable
        {
            public Image<Rgba32> Image { get; }
            public MemoryHandle Handle { get; }
            public ImagePixelOwner(Image<Rgba32> image) { Image = image; Handle = image.Frames.RootFrame.DangerousTryGetSinglePixelMemory(out var memory) ? memory.Pin() : throw new InvalidOperationException("Unable to pin pixel memory."); }
            public void Dispose() { Handle.Dispose(); Image.Dispose(); }
        }
        private static unsafe SKBitmap ImageSharpToSkBitmap(Image<Rgba32> img)
        {
            var info = new SKImageInfo(img.Width, img.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var owner = new ImagePixelOwner(img);
            var bmp = new SKBitmap();
            bmp.InstallPixels(info, (IntPtr)owner.Handle.Pointer, img.Width * Unsafe.SizeOf<Rgba32>(), (addr, ctx) => ((ImagePixelOwner)ctx!).Dispose(), owner);
            return bmp;
        }
        private async Task SaveTileToDiskAsync(int cellSize, int tileX, int tileY, SKBitmap bmp, CancellationToken token)
        {
            string dir = Path.Combine(TileCacheDir, cellSize.ToString());
            string path = Path.Combine(dir, $"{tileX}_{tileY}.png");
            try
            {
                Directory.CreateDirectory(dir);
                var fileLock = GetFileLock(path);
                await fileLock.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    using var image = SKImage.FromBitmap(bmp);
                    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                    await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true);
                    data.SaveTo(fs);
                }
                finally { fileLock.Release(); }
            }
            catch (Exception ex) { Debug.WriteLine($"[ASYNC TILE SAVE ERROR] {ex.Message} while saving {path}"); }
        }
        private async Task<SKBitmap> LoadOrGenerateTileFromDataAsync(int cellSize, int tileX, int tileY, CancellationToken token)
        {
            using var imageSharpImage = await Task.Run(() => PixelMapGenerator.GenerateTileWithCountriesLarge(_baseWidth, _baseHeight, cellSize, tileX, tileY), token).ConfigureAwait(false);
            if (imageSharpImage == null) return new SKBitmap(TileSizePx, TileSizePx);
            return ImageSharpToSkBitmap(imageSharpImage);
        }
    }
}