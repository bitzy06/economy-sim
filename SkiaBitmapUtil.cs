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
            if (bmp == null || bmp.Width <= 0 || bmp.Height <= 0)
                return new SKBitmap(1, 1);

            Bitmap src = bmp;
            Bitmap? converted = null;
            PixelFormat fmt = bmp.PixelFormat;
            if (fmt != PixelFormat.Format32bppArgb && fmt != PixelFormat.Format32bppPArgb)
            {
                converted = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(converted))
                    g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height);
                src = converted;
                fmt = PixelFormat.Format32bppArgb;
            }

            var rect = new Rectangle(0, 0, src.Width, src.Height);
            var data = src.LockBits(rect, ImageLockMode.ReadOnly, fmt);
            try
            {
                var info = new SKImageInfo(src.Width, src.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var sk = new SKBitmap(info);
                byte* dst = (byte*)sk.GetPixels().ToPointer();
                for (int y = 0; y < info.Height; y++)
                {
                    byte* srcRow = (byte*)data.Scan0 + y * data.Stride;
                    byte* dstRow = dst + y * sk.Info.RowBytes;
                    Buffer.MemoryCopy(srcRow, dstRow, sk.Info.RowBytes, info.BytesPerPixel * info.Width);
                }
                return sk;
            }
            finally
            {
                src.UnlockBits(data);
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
