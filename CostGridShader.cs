using ComputeSharp;

namespace StrategyGame
{
    [ThreadGroupSize(8, 8, 1)]
    internal readonly partial struct CostGridShader : IComputeShader
    {
        public readonly ReadOnlyTexture2D<float> elevation;
        public readonly ReadOnlyTexture2D<float> water;
        public readonly ReadWriteTexture2D<float> cost;

        public CostGridShader(ReadOnlyTexture2D<float> elevation,
                               ReadOnlyTexture2D<float> water,
                               ReadWriteTexture2D<float> cost)
        {
            this.elevation = elevation;
            this.water = water;
            this.cost = cost;
        }

        public void Execute()
        {
            int x = ThreadIds.X;
            int y = ThreadIds.Y;
            float eCenter = elevation[x, y];
            float slope = 0f;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (ox == 0 && oy == 0) continue;
                    int nx = Hlsl.Clamp(x + ox, 0, elevation.Width - 1);
                    int ny = Hlsl.Clamp(y + oy, 0, elevation.Height - 1);
                    float e = elevation[nx, ny];
                    slope += Hlsl.Abs(e - eCenter);
                }
            }
            slope /= 8f;
            float waterCost = water[x, y];
            cost[x, y] = slope * 50f + waterCost * 1000f;
        }
    }
}
