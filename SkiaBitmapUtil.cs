using SkiaSharp;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace StrategyGame
{
    internal static class SkiaBitmapUtil
    {
        public static SKBitmap ToSKBitmap(Bitmap bmp)
        {
            if (bmp == null || bmp.Width <= 0 || bmp.Height <= 0)
                return new SKBitmap(1, 1); // avoid GDI+ errors on invalid input

            using var ms = new MemoryStream();
            // Saving as BMP is broadly supported even when libgdiplus is used on
            // non‑Windows platforms, preventing 'Generic error in GDI+' issues
            // that sometimes occur with the PNG encoder.
            bmp.Save(ms, ImageFormat.Bmp);
            ms.Position = 0;
            return SKBitmap.Decode(ms);
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
