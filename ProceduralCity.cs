using System.Collections.Generic;

namespace StrategyGame
{
    /// <summary>
    /// Represents a procedural city location and provides
    /// on-demand generation of simple road and building layouts.
    /// </summary>
    public class ProceduralCity
    {
        public double WorldX { get; }
        public double WorldY { get; }

        public ProceduralCity(double x, double y)
        {
            WorldX = x;
            WorldY = y;
        }

        public List<LineSegment> GetRoads(int cellSize)
        {
            var roads = new List<LineSegment>();
            if (cellSize < 10)
                return roads;

            double span = 0.08; // degrees covered by the city grid
            double step = span / 4.0;
            for (int i = 0; i <= 4; i++)
            {
                double offset = -span / 2.0 + i * step;
                // horizontal
                roads.Add(new LineSegment(
                    WorldX - span / 2.0,
                    WorldY + offset,
                    WorldX + span / 2.0,
                    WorldY + offset));
                // vertical
                roads.Add(new LineSegment(
                    WorldX + offset,
                    WorldY - span / 2.0,
                    WorldX + offset,
                    WorldY + span / 2.0));
            }
            return roads;
        }

        public List<Polygon> GetBuildingFootprints(int cellSize)
        {
            var buildings = new List<Polygon>();
            if (cellSize < 40)
                return buildings;

            double span = 0.08;
            double step = span / 4.0;
            double pad = step * 0.1;
            for (int xi = 0; xi < 4; xi++)
            {
                for (int yi = 0; yi < 4; yi++)
                {
                    double minX = WorldX - span / 2.0 + xi * step + pad;
                    double maxX = WorldX - span / 2.0 + (xi + 1) * step - pad;
                    double minY = WorldY - span / 2.0 + yi * step + pad;
                    double maxY = WorldY - span / 2.0 + (yi + 1) * step - pad;
                    buildings.Add(new Polygon(new List<(double X, double Y)>
                    {
                        (minX, minY),
                        (maxX, minY),
                        (maxX, maxY),
                        (minX, maxY)
                    }));
                }
            }
            return buildings;
        }
    }

    /// <summary>
    /// Simple line segment data structure.
    /// </summary>
    public struct LineSegment
    {
        public double X1 { get; }
        public double Y1 { get; }
        public double X2 { get; }
        public double Y2 { get; }

        public LineSegment(double x1, double y1, double x2, double y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }
    }

    /// <summary>
    /// Basic polygon made up of ordered vertices.
    /// </summary>
    public class Polygon
    {
        public List<(double X, double Y)> Vertices { get; }

        public Polygon(List<(double X, double Y)> vertices)
        {
            Vertices = vertices;
        }
    }
}

