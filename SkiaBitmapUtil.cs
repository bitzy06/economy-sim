using SkiaSharp;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace StrategyGame
{
    internal static class SkiaBitmapUtil
    {
        public static unsafe SKBitmap ToSKBitmap(Bitmap bmp)
        {
            if (bmp == null)
                return new SKBitmap(1, 1);

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
                    return new SKBitmap(1, 1);
            }
            catch (ArgumentException)
            {
                // Bitmap was disposed or corrupted
                return new SKBitmap(1, 1);
            }
            catch (InvalidOperationException)
            {
                // Bitmap is being used by another thread
                return new SKBitmap(1, 1);
            }

            Bitmap src = bmp;
            Bitmap? converted = null;
            PixelFormat fmt = pixelFormat;
            
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
                    return new SKBitmap(1, 1);
                }
                catch (ArgumentException)
                {
                    // Source bitmap was disposed
                    converted?.Dispose();
                    return new SKBitmap(1, 1);
                }
            }

            var rect = new Rectangle(0, 0, src.Width, src.Height);
            BitmapData? data = null;
            
            try
            {
                // Lock the bitmap for reading - this is the critical section
                data = src.LockBits(rect, ImageLockMode.ReadOnly, fmt);
                
                // Validate the locked data
                if (data.Scan0 == IntPtr.Zero || data.Stride <= 0)
                {
                    return new SKBitmap(1, 1);
                }
                
                var info = new SKImageInfo(src.Width, src.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var sk = new SKBitmap(info);
                
                if (sk.GetPixels() == IntPtr.Zero)
                {
                    sk.Dispose();
                    return new SKBitmap(1, 1);
                }
                
                byte* dst = (byte*)sk.GetPixels().ToPointer();
                
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
                        return new SKBitmap(1, 1);
                    }
                }
                
                return sk;
            }
            catch (ArgumentException)
            {
                // Bitmap was disposed or parameters were invalid
                return new SKBitmap(1, 1);
            }
            catch (InvalidOperationException)
            {
                // Bitmap is being used by another thread or was disposed
                return new SKBitmap(1, 1);
            }
            catch (AccessViolationException)
            {
                // Memory access violation during LockBits
                return new SKBitmap(1, 1);
            }
            catch (OutOfMemoryException)
            {
                // Not enough memory to lock bitmap
                return new SKBitmap(1, 1);
            }
            finally
            {
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
            }
        }

        public static Bitmap ToGdiBitmap(SKBitmap skBmp)
        {
            using var image = SKImage.FromBitmap(skBmp);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var ms = data.AsStream();
            return new Bitmap(ms);
        }
    }
}
