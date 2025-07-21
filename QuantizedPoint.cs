using System;

namespace StrategyGame
{
    public readonly struct QuantizedPoint : IEquatable<QuantizedPoint>
    {
        private const int Precision = 8;
        public double X { get; }
        public double Y { get; }

        public QuantizedPoint(double x, double y)
        {
            X = Math.Round(x, Precision);
            Y = Math.Round(y, Precision);
        }

        public bool Equals(QuantizedPoint other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is QuantizedPoint other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
    }
}
