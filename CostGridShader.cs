using ComputeSharp;
using ComputeSharp; // for attributes

namespace StrategyGame
{
    [AutoConstructor]
    public readonly partial struct CostGridShader : IComputeShader
    {
        public readonly ReadOnlyTexture2D<float> elevation;
        public readonly ReadOnlyTexture2D<float> water;
        public readonly ReadWriteTexture2D<float> cost;

        public void Execute()
        {
            int x = ThreadIds.X;
            int y = ThreadIds.Y;
            float elev = elevation[x, y];
            float waterValue = water[x, y];
            float slope = 0f;
            int width = elevation.Width;
            int height = elevation.Height;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    int nx = Hlsl.Clamp(x + ox, 0, width - 1);
                    int ny = Hlsl.Clamp(y + oy, 0, height - 1);
                    slope += Hlsl.Abs(elevation[nx, ny] - elev);
                }
            }
            slope /= 8f;
            float costVal = slope * 50f + waterValue * 1000f;
            cost[x, y] = costVal;
        }
    }
}
