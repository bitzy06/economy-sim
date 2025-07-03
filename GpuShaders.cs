using ComputeSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace economy_sim;

[AutoConstructor]
public readonly partial struct TerrainTileShader : IComputeShader
{
    public readonly ReadOnlyBuffer<byte> r;
    public readonly ReadOnlyBuffer<byte> g;
    public readonly ReadOnlyBuffer<byte> b;
    public readonly ReadOnlyBuffer<int> landMask;
    public readonly ReadWriteTexture2D<Rgba32> output;
    public readonly int cellSize;
    public readonly int cellsX;
    public readonly int tileWidth;
    public readonly int tileHeight;

    public void Execute()
    {
        int x = ThreadIds.X;
        int y = ThreadIds.Y;
        if (x >= tileWidth || y >= tileHeight) return;

        int cellX = x / cellSize;
        int cellY = y / cellSize;
        int idx = cellY * cellsX + cellX;

        bool isLand = landMask[y * tileWidth + x] != 0;
        Rgba32 water = new Rgba32(135, 206, 250, 255);

        if (!isLand)
        {
            output[x, y] = water;
            return;
        }

        Rgba32 baseColor = new Rgba32(r[idx], g[idx], b[idx], 255);
        Rgba32 dark = Lerp(baseColor, new Rgba32(0, 0, 0, 255), 0.2f);
        Rgba32 light = Lerp(baseColor, new Rgba32(255, 255, 255, 255), 0.2f);

        uint seed = unchecked((uint)(x + y * tileWidth));
        seed = seed * 1664525u + 1013904223u;
        int choice = (int)((seed >> 24) % 3u);
        Rgba32 color = choice == 0 ? dark : (choice == 1 ? baseColor : light);
        output[x, y] = color;
    }

    private static Rgba32 Lerp(Rgba32 a, Rgba32 b, float t)
    {
        return new Rgba32(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }
}

