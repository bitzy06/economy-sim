using SkiaSharp;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;
using economy_sim;

namespace StrategyGame
{
    internal static class SkiaBitmapUtil
    {
        public static unsafe SKBitmap ToSKBitmap(Bitmap bmp)
        {
            var sw = Stopwatch.StartNew();
            
            if (bmp == null)
            {
                PerformanceTracker.Record("ToSKBitmap-NullBitmap", sw.Elapsed);
                return new SKBitmap(1, 1);
            }

            // Thread-safe validation: check if bitmap is accessible before proceeding
            int width, height;
            PixelFormat pixelFormat;
            
            try
            {
                // These property accesses can throw if bitmap is disposed or being modified
                width = bmp.Width;
                height = bmp.Height;
                pixelFormat = bmp.PixelFormat;
                
                if (width <= 0 || height <= 0)
                {
                    PerformanceTracker.Record("ToSKBitmap-InvalidDimensions", sw.Elapsed);
                    return new SKBitmap(1, 1);
                }
            }
            catch (ArgumentException)
            {
                // Bitmap was disposed or corrupted
                PerformanceTracker.Record("ToSKBitmap-ArgumentException", sw.Elapsed);
                return new SKBitmap(1, 1);
            }
            catch (InvalidOperationException)
            {
                // Bitmap is being used by another thread
                PerformanceTracker.Record("ToSKBitmap-InvalidOperationException", sw.Elapsed);
                return new SKBitmap(1, 1);
            }

            Bitmap src = bmp;
            Bitmap? converted = null;
            PixelFormat fmt = pixelFormat;
            
            var swConversion = Stopwatch.StartNew();
            // Convert to compatible format if needed
            if (fmt != PixelFormat.Format32bppArgb && fmt != PixelFormat.Format32bppPArgb)
            {
                try
                {
                    converted = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                    using (var g = Graphics.FromImage(converted))
                        g.DrawImage(bmp, 0, 0, width, height);
                    src = converted;
                    fmt = PixelFormat.Format32bppArgb;
                }
                catch (InvalidOperationException)
                {
                    // Source bitmap became invalid during conversion
                    converted?.Dispose();
                    PerformanceTracker.Record("ToSKBitmap-ConversionFailed-InvalidOp", swConversion.Elapsed);
                    PerformanceTracker.Record("ToSKBitmap", sw.Elapsed);
                    return new SKBitmap(1, 1);
                }
                catch (ArgumentException)
                {
                    // Source bitmap was disposed
                    converted?.Dispose();
                    PerformanceTracker.Record("ToSKBitmap-ConversionFailed-ArgEx", swConversion.Elapsed);
                    PerformanceTracker.Record("ToSKBitmap", sw.Elapsed);
                    return new SKBitmap(1, 1);
                }
            }
            PerformanceTracker.Record("ToSKBitmap-FormatConversion", swConversion.Elapsed);

            var rect = new Rectangle(0, 0, src.Width, src.Height);
            BitmapData? data = null;
            
            var swLock = Stopwatch.StartNew();
            try
            {
                // Lock the bitmap for reading - this is the critical section
                data = src.LockBits(rect, ImageLockMode.ReadOnly, fmt);
                
                // Validate the locked data
                if (data.Scan0 == IntPtr.Zero || data.Stride <= 0)
                {
                    PerformanceTracker.Record("ToSKBitmap-InvalidLockedData", swLock.Elapsed);
                    PerformanceTracker.Record("ToSKBitmap", sw.Elapsed);
                    return new SKBitmap(1, 1);
                }
                
                var info = new SKImageInfo(src.Width, src.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var sk = new SKBitmap(info);
                
                if (sk.GetPixels() == IntPtr.Zero)
                {
                    sk.Dispose();
                    PerformanceTracker.Record("ToSKBitmap-SKBitmapAllocationFailed", swLock.Elapsed);
                    PerformanceTracker.Record("ToSKBitmap", sw.Elapsed);
                    return new SKBitmap(1, 1);
                }
                
                byte* dst = (byte*)sk.GetPixels().ToPointer();
                
                var swCopy = Stopwatch.StartNew();
                // Copy data row by row with validation
                for (int y = 0; y < info.Height; y++)
                {
                    try
                    {
                        byte* srcRow = (byte*)data.Scan0 + y * data.Stride;
                        byte* dstRow = dst + y * sk.Info.RowBytes;
                        int bytesToCopy = info.BytesPerPixel * info.Width;
                        
                        // Ensure we don't copy more bytes than available in either buffer
                        int maxSrcBytes = data.Stride;
                        int maxDstBytes = sk.Info.RowBytes;
                        bytesToCopy = Math.Min(bytesToCopy, Math.Min(maxSrcBytes, maxDstBytes));
                        
                        if (bytesToCopy > 0)
                        {
                            Buffer.MemoryCopy(srcRow, dstRow, maxDstBytes, bytesToCopy);
                        }
                    }
                    catch (AccessViolationException)
                    {
                        // Memory access failed - bitmap may have been disposed during copy
                        sk.Dispose();
                        PerformanceTracker.Record("ToSKBitmap-MemoryCopyFailed", swCopy.Elapsed);
                        PerformanceTracker.Record("ToSKBitmap", sw.Elapsed);
                        return new SKBitmap(1, 1);
                    }
                }
                PerformanceTracker.Record("ToSKBitmap-MemoryCopy", swCopy.Elapsed);
                PerformanceTracker.Record("ToSKBitmap", sw.Elapsed);
                return sk;
            }
            catch (ArgumentException)
            {
                // Bitmap was disposed or parameters were invalid
                PerformanceTracker.Record("ToSKBitmap-LockFailed-ArgEx", swLock.Elapsed);
                PerformanceTracker.Record("ToSKBitmap", sw.Elapsed);
                return new SKBitmap(1, 1);
            }
            catch (InvalidOperationException)
            {
                // Bitmap is being used by another thread or was disposed
                PerformanceTracker.Record("ToSKBitmap-LockFailed-InvalidOp", swLock.Elapsed);
                PerformanceTracker.Record("ToSKBitmap", sw.Elapsed);
                return new SKBitmap(1, 1);
            }
            catch (AccessViolationException)
            {
                // Memory access violation during LockBits
                PerformanceTracker.Record("ToSKBitmap-LockFailed-AccessViolation", swLock.Elapsed);
                PerformanceTracker.Record("ToSKBitmap", sw.Elapsed);
                return new SKBitmap(1, 1);
            }
            catch (OutOfMemoryException)
            {
                // Not enough memory to lock bitmap
                PerformanceTracker.Record("ToSKBitmap-LockFailed-OutOfMemory", swLock.Elapsed);
                PerformanceTracker.Record("ToSKBitmap", sw.Elapsed);
                return new SKBitmap(1, 1);
            }
            finally
            {
                var swUnlock = Stopwatch.StartNew();
                // Always unlock the bitmap if it was locked
                if (data != null)
                {
                    try
                    {
                        src.UnlockBits(data);
                    }
                    catch
                    {
                        // Ignore errors during unlock - bitmap may already be disposed
                    }
                }
                
                // Clean up converted bitmap
                converted?.Dispose();
                PerformanceTracker.Record("ToSKBitmap-Unlock", swUnlock.Elapsed);
            }
        }

        public static Bitmap ToGdiBitmap(SKBitmap skBmp)
        {
            var sw = Stopwatch.StartNew();
            using var image = SKImage.FromBitmap(skBmp);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = data.AsStream();
            var result = new Bitmap(ms);
            PerformanceTracker.Record("ToGdiBitmap", sw.Elapsed);
            return result;
        }
    }
}
