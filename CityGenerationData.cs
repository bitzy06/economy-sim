using System;

namespace StrategyGame
{
    /// <summary>
    /// Container for global data used when procedurally generating cities.
    /// </summary>
    public class CityGenerationData
    {
        public float[,] PopulationDensity { get; }
        public bool[,] WaterBodies { get; }
        public float[,] Elevation { get; }

        public CityGenerationData(float[,] populationDensity, bool[,] waterBodies, float[,] elevation)
        {
            PopulationDensity = populationDensity ?? throw new ArgumentNullException(nameof(populationDensity));
            WaterBodies = waterBodies ?? throw new ArgumentNullException(nameof(waterBodies));
            Elevation = elevation ?? throw new ArgumentNullException(nameof(elevation));
        }

        private static int ClampIndex(double value, int size) => (int)Math.Clamp(value * (size - 1), 0, size - 1);

        public float GetPopulationDensity(double xNorm, double yNorm)
        {
            int ix = ClampIndex(xNorm, PopulationDensity.GetLength(0));
            int iy = ClampIndex(yNorm, PopulationDensity.GetLength(1));
            return PopulationDensity[ix, iy];
        }

        public bool IsWater(double xNorm, double yNorm)
        {
            int ix = ClampIndex(xNorm, WaterBodies.GetLength(0));
            int iy = ClampIndex(yNorm, WaterBodies.GetLength(1));
            return WaterBodies[ix, iy];
        }

        public float GetElevation(double xNorm, double yNorm)
        {
            int ix = ClampIndex(xNorm, Elevation.GetLength(0));
            int iy = ClampIndex(yNorm, Elevation.GetLength(1));
            return Elevation[ix, iy];
        }
    }
}
