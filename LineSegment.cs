namespace StrategyGame
{
    /// <summary>
    /// Types of roads used for procedural generation.
    /// </summary>
    public enum RoadType { Primary, Secondary }

    /// <summary>
    /// Lightweight representation of a road segment with an associated type.
    /// </summary>
    public struct LineSegment
    {
        public double X1, Y1, X2, Y2;
        public RoadType Type;

        public LineSegment(double x1, double y1, double x2, double y2, RoadType type = RoadType.Secondary)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
            Type = type;
        }
    }
}
