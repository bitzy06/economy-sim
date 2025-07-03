using System;

namespace StrategyGame
{
    public class PopulationDensityMap
    {
        public float[,] Values { get; }
        public int Width => Values.GetLength(0);
        public int Height => Values.GetLength(1);

        public PopulationDensityMap(float[,] values)
        {
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public float GetDensity(double xNorm, double yNorm)
        {
            int ix = (int)Math.Clamp(xNorm * (Width - 1), 0, Width - 1);
            int iy = (int)Math.Clamp(yNorm * (Height - 1), 0, Height - 1);
            return Values[ix, iy];
        }
    }

    public class WaterBodyMap
    {
        public bool[,] Values { get; }
        public int Width => Values.GetLength(0);
        public int Height => Values.GetLength(1);

        public WaterBodyMap(bool[,] values)
        {
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        public bool IsWater(double xNorm, double yNorm)
        {
            int ix = (int)Math.Clamp(xNorm * (Width - 1), 0, Width - 1);
            int iy = (int)Math.Clamp(yNorm * (Height - 1), 0, Height - 1);
            return Values[ix, iy];
        }
    }

    public class TerrainData
    {
        public float[,] Elevation { get; }
        public int Width => Elevation.GetLength(0);
        public int Height => Elevation.GetLength(1);

        public TerrainData(float[,] elevation)
        {
            Elevation = elevation ?? throw new ArgumentNullException(nameof(elevation));
        }

        public float GetElevation(double xNorm, double yNorm)
        {
            int ix = (int)Math.Clamp(xNorm * (Width - 1), 0, Width - 1);
            int iy = (int)Math.Clamp(yNorm * (Height - 1), 0, Height - 1);
            return Elevation[ix, iy];
        }
    }
}
