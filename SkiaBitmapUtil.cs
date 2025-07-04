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
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
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
