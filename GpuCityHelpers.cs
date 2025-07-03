using ComputeSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;

namespace StrategyGame
{
    /// <summary>
    /// Helper methods for GPU accelerated city tile generation.
    /// Uses ComputeSharp to fill pixel data in parallel on the GPU.
    /// Falls back to CPU when a compatible device is not available.
    /// </summary>
    public static class GpuCityHelpers
    {
        [AutoConstructor]
        private readonly partial struct FillTileShader : IComputeShader
        {
            public readonly ReadOnlyBuffer<byte> r;
            public readonly ReadOnlyBuffer<byte> g;
            public readonly ReadOnlyBuffer<byte> b;
            public readonly ReadOnlyBuffer<int> mask;
            public readonly int cellsX;
            public readonly int cellSize;
            public readonly Rgba32 water;
            public readonly ReadWriteTexture2D<Rgba32> dest;

            public void Execute()
            {
                int x = ThreadIds.X;
                int y = ThreadIds.Y;
                int cellX = x / cellSize;
                int cellY = y / cellSize;
                int idx = cellY * cellsX + cellX;
                bool land = mask[x + y * dest.Width] != 0;
                var color = new Rgba32(r[idx], g[idx], b[idx], 255);
                dest[x, y] = land ? color : water;
            }
        }

        /// <summary>
        /// Attempt to generate a terrain tile using GPU acceleration.
        /// Returns true if successful, false if GPU is unavailable.
        /// </summary>
        public static bool TryGenerateTile(int tileWidth, int tileHeight, int cellSize,
            byte[] r, byte[] g, byte[] b, int[,] landMask, out Image<Rgba32> img)
        {
            img = null;
            try
            {
                GraphicsDevice device = GraphicsDevice.GetDefault();
                int cellsX = (tileWidth + cellSize - 1) / cellSize;
                int cellsY = (tileHeight + cellSize - 1) / cellSize;
                int[] maskFlat = new int[tileWidth * tileHeight];
                for (int y = 0; y < tileHeight; y++)
                {
                    for (int x = 0; x < tileWidth; x++)
                    {
                        maskFlat[y * tileWidth + x] = landMask[y, x];
                    }
                }
                using ReadOnlyBuffer<byte> rBuf = device.AllocateReadOnlyBuffer(r);
                using ReadOnlyBuffer<byte> gBuf = device.AllocateReadOnlyBuffer(g);
                using ReadOnlyBuffer<byte> bBuf = device.AllocateReadOnlyBuffer(b);
                using ReadOnlyBuffer<int> maskBuf = device.AllocateReadOnlyBuffer(maskFlat);
                using ReadWriteTexture2D<Rgba32> dest = device.AllocateReadWriteTexture2D<Rgba32>(tileWidth, tileHeight);

                device.For(tileWidth, tileHeight, new FillTileShader(rBuf, gBuf, bBuf, maskBuf, cellsX, cellSize,
                    new Rgba32(135, 206, 250, 255), dest));

                var pixels = new Rgba32[tileWidth * tileHeight];
                dest.CopyTo(pixels);
                img = Image.LoadPixelData(pixels.AsSpan(), tileWidth, tileHeight);
                return true;
            }
            catch
            {
                img = null;
                return false;
            }
        }
    }
}
