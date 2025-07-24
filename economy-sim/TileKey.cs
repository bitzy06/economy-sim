using System;

namespace StrategyGame
{
    public readonly struct TileKey : IEquatable<TileKey>
    {
        public Guid ModelId { get; }
        public int CellSize { get; }
        public int TileX { get; }
        public int TileY { get; }

        public TileKey(Guid modelId, int cellSize, int tileX, int tileY)
        {
            ModelId = modelId;
            CellSize = cellSize;
            TileX = tileX;
            TileY = tileY;
        }

        public bool Equals(TileKey other)
        {
            return ModelId.Equals(other.ModelId) &&
                   CellSize == other.CellSize &&
                   TileX == other.TileX &&
                   TileY == other.TileY;
        }

        public override bool Equals(object obj)
        {
            return obj is TileKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ModelId, CellSize, TileX, TileY);
        }
    }
}
