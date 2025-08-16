using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace Economy_sim
{
    /// <summary>
    /// Core grid-based political control engine optimized for dynamic war/occupation mechanics.
    /// Replaces polygon-first rendering with an authoritative world control grid.
    /// </summary>
    public sealed class GridControlEngine : IDisposable
    {
        public readonly int Width;
        public readonly int Height; 
        public readonly int TileSize;

        // Core grid layers
        public int[,] BaseOwnerGrid { get; private set; }  // Immutable borders from initial rasterization
        public int[,] ControlGrid { get; private set; }    // Current controller (starts as copy of base)
        
        // Optional layers for advanced features
        public bool[,]? FrontlineGrid { get; private set; } // Contested areas
        public int[,]? StateGrid { get; private set; }      // Intrastate control

        // Tile management
        private readonly int _tilesX;
        private readonly int _tilesY;
        private readonly bool[,] _dirtyTiles;
        private readonly object _dirtyLock = new object();

        // LOD support
        private readonly Dictionary<int, int[,]> _lodGrids = new Dictionary<int, int[,]>();
        private readonly object _lodLock = new object();

        // Events for change notification
        public event EventHandler<GridChangedEventArgs>? GridChanged;
        public event EventHandler<TileChangedEventArgs>? TileChanged;

        public GridControlEngine(int width = 8192, int height = 4096, int tileSize = 512)
        {
            Width = width;
            Height = height;
            TileSize = tileSize;

            _tilesX = (int)Math.Ceiling((double)width / tileSize);
            _tilesY = (int)Math.Ceiling((double)height / tileSize);

            // Initialize grids
            BaseOwnerGrid = new int[height, width];
            ControlGrid = new int[height, width];
            _dirtyTiles = new bool[_tilesY, _tilesX];

            // Mark all tiles as dirty initially
            MarkAllTilesDirty();
        }

        /// <summary>
        /// Initialize the base owner grid from external data (e.g., shapefile rasterization)
        /// </summary>
        public void InitializeBaseGrid(int[,] sourceGrid)
        {
            if (sourceGrid.GetLength(0) != Height || sourceGrid.GetLength(1) != Width)
                throw new ArgumentException("Source grid dimensions must match engine dimensions");

            // Copy base grid
            Array.Copy(sourceGrid, BaseOwnerGrid, sourceGrid.Length);
            
            // Initialize control grid as copy of base
            Array.Copy(sourceGrid, ControlGrid, sourceGrid.Length);

            // Invalidate all LOD levels
            InvalidateAllLods();
            MarkAllTilesDirty();

            GridChanged?.Invoke(this, new GridChangedEventArgs(GridChangeType.FullReset));
        }

        /// <summary>
        /// Get tile coordinates for a given cell position
        /// </summary>
        public (int tileX, int tileY) GetTileCoordinates(int cellX, int cellY)
        {
            return (cellX / TileSize, cellY / TileSize);
        }

        /// <summary>
        /// Get tile coordinates for a set of cells
        /// </summary>
        public IEnumerable<(int tileX, int tileY)> GetTileCoordinates(IEnumerable<Point> cells)
        {
            return cells.Select(cell => GetTileCoordinates(cell.X, cell.Y)).Distinct();
        }

        /// <summary>
        /// Mark specific cells as dirty
        /// </summary>
        public void MarkCellsDirty(IEnumerable<Point> cells)
        {
            var tiles = GetTileCoordinates(cells).ToList();
            MarkTilesDirty(tiles);
        }

        /// <summary>
        /// Mark a rectangular region as dirty
        /// </summary>
        public void MarkRegionDirty(Rectangle region)
        {
            var cells = new List<Point>();
            for (int y = region.Y; y < region.Y + region.Height && y < Height; y++)
            {
                for (int x = region.X; x < region.X + region.Width && x < Width; x++)
                {
                    cells.Add(new Point(x, y));
                }
            }
            MarkCellsDirty(cells);
        }

        /// <summary>
        /// Apply ownership change to a set of cells
        /// </summary>
        public void ChangeControl(int countryId, IEnumerable<Point> cells)
        {
            var cellList = cells.ToList();
            var affectedTiles = new HashSet<(int, int)>();

            foreach (var cell in cellList)
            {
                if (cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height)
                {
                    ControlGrid[cell.Y, cell.X] = countryId;
                    affectedTiles.Add(GetTileCoordinates(cell.X, cell.Y));
                }
            }

            // Mark affected tiles as dirty
            MarkTilesDirty(affectedTiles);

            // Invalidate affected LOD levels
            InvalidateLodsForTiles(affectedTiles);

            GridChanged?.Invoke(this, new GridChangedEventArgs(GridChangeType.CellUpdate, cellList));
        }

        /// <summary>
        /// Flood fill control from a seed point
        /// </summary>
        public void FloodFillControl(Point seed, int newCountryId, Func<int, bool> canReplace)
        {
            if (seed.X < 0 || seed.X >= Width || seed.Y < 0 || seed.Y >= Height)
                return;

            var visited = new bool[Height, Width];
            var queue = new Queue<Point>();
            var affectedCells = new List<Point>();
            var affectedTiles = new HashSet<(int, int)>();

            queue.Enqueue(seed);
            visited[seed.Y, seed.X] = true;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int currentId = ControlGrid[current.Y, current.X];

                if (canReplace(currentId))
                {
                    ControlGrid[current.Y, current.X] = newCountryId;
                    affectedCells.Add(current);
                    affectedTiles.Add(GetTileCoordinates(current.X, current.Y));

                    // Check 4-connected neighbors
                    var neighbors = new[]
                    {
                        new Point(current.X + 1, current.Y),
                        new Point(current.X - 1, current.Y),
                        new Point(current.X, current.Y + 1),
                        new Point(current.X, current.Y - 1)
                    };

                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor.X >= 0 && neighbor.X < Width && 
                            neighbor.Y >= 0 && neighbor.Y < Height &&
                            !visited[neighbor.Y, neighbor.X])
                        {
                            visited[neighbor.Y, neighbor.X] = true;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }

            if (affectedCells.Count > 0)
            {
                MarkTilesDirty(affectedTiles);
                InvalidateLodsForTiles(affectedTiles);
                GridChanged?.Invoke(this, new GridChangedEventArgs(GridChangeType.FloodFill, affectedCells));
            }
        }

        /// <summary>
        /// Compute frontline cells (differences between base and control grids)
        /// </summary>
        public IReadOnlyList<Point> ComputeFrontline()
        {
            var frontline = new List<Point>();

            Parallel.For(0, Height, y =>
            {
                var localFrontline = new List<Point>();
                for (int x = 0; x < Width; x++)
                {
                    if (BaseOwnerGrid[y, x] != ControlGrid[y, x])
                    {
                        localFrontline.Add(new Point(x, y));
                    }
                }

                lock (frontline)
                {
                    frontline.AddRange(localFrontline);
                }
            });

            return frontline.AsReadOnly();
        }

        /// <summary>
        /// Get control grid at specific LOD level (0 = base level)
        /// </summary>
        public int[,] GetControlGridLod(int lodLevel)
        {
            if (lodLevel == 0)
                return ControlGrid;

            lock (_lodLock)
            {
                if (_lodGrids.TryGetValue(lodLevel, out var lodGrid))
                    return lodGrid;

                // Generate LOD level
                lodGrid = GenerateLodLevel(lodLevel);
                _lodGrids[lodLevel] = lodGrid;
                return lodGrid;
            }
        }

        /// <summary>
        /// Check if any tiles are dirty
        /// </summary>
        public bool HasDirtyTiles()
        {
            lock (_dirtyLock)
            {
                for (int y = 0; y < _tilesY; y++)
                {
                    for (int x = 0; x < _tilesX; x++)
                    {
                        if (_dirtyTiles[y, x])
                            return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Get all dirty tile coordinates and optionally clear them
        /// </summary>
        public IEnumerable<(int tileX, int tileY)> GetDirtyTiles(bool clearAfterGet = false)
        {
            var dirtyTiles = new List<(int, int)>();

            lock (_dirtyLock)
            {
                for (int y = 0; y < _tilesY; y++)
                {
                    for (int x = 0; x < _tilesX; x++)
                    {
                        if (_dirtyTiles[y, x])
                        {
                            dirtyTiles.Add((x, y));
                            if (clearAfterGet)
                                _dirtyTiles[y, x] = false;
                        }
                    }
                }
            }

            return dirtyTiles;
        }

        private void MarkTilesDirty(IEnumerable<(int tileX, int tileY)> tiles)
        {
            lock (_dirtyLock)
            {
                foreach (var (tileX, tileY) in tiles)
                {
                    if (tileX >= 0 && tileX < _tilesX && tileY >= 0 && tileY < _tilesY)
                    {
                        _dirtyTiles[tileY, tileX] = true;
                        TileChanged?.Invoke(this, new TileChangedEventArgs(tileX, tileY));
                    }
                }
            }
        }

        private void MarkAllTilesDirty()
        {
            lock (_dirtyLock)
            {
                for (int y = 0; y < _tilesY; y++)
                {
                    for (int x = 0; x < _tilesX; x++)
                    {
                        _dirtyTiles[y, x] = true;
                    }
                }
            }
        }

        private int[,] GenerateLodLevel(int lodLevel)
        {
            int scale = 1 << lodLevel; // 2^lodLevel
            int lodWidth = Width / scale;
            int lodHeight = Height / scale;
            
            var lodGrid = new int[lodHeight, lodWidth];

            Parallel.For(0, lodHeight, y =>
            {
                for (int x = 0; x < lodWidth; x++)
                {
                    // Sample from base level using majority vote
                    var counts = new Dictionary<int, int>();
                    
                    for (int dy = 0; dy < scale && y * scale + dy < Height; dy++)
                    {
                        for (int dx = 0; dx < scale && x * scale + dx < Width; dx++)
                        {
                            int value = ControlGrid[y * scale + dy, x * scale + dx];
                            counts[value] = counts.GetValueOrDefault(value, 0) + 1;
                        }
                    }

                    // Find majority value
                    lodGrid[y, x] = counts.MaxBy(kvp => kvp.Value).Key;
                }
            });

            return lodGrid;
        }

        private void InvalidateAllLods()
        {
            lock (_lodLock)
            {
                _lodGrids.Clear();
            }
        }

        private void InvalidateLodsForTiles(IEnumerable<(int tileX, int tileY)> tiles)
        {
            // For now, just invalidate all LODs when any tiles change
            // TODO: Implement more granular LOD invalidation
            InvalidateAllLods();
        }

        public void Dispose()
        {
            lock (_lodLock)
            {
                _lodGrids.Clear();
            }
        }
    }

    /// <summary>
    /// Event arguments for grid changes
    /// </summary>
    public class GridChangedEventArgs : EventArgs
    {
        public GridChangeType ChangeType { get; }
        public IReadOnlyList<Point>? AffectedCells { get; }

        public GridChangedEventArgs(GridChangeType changeType, IReadOnlyList<Point>? affectedCells = null)
        {
            ChangeType = changeType;
            AffectedCells = affectedCells;
        }
    }

    /// <summary>
    /// Event arguments for tile changes
    /// </summary>
    public class TileChangedEventArgs : EventArgs
    {
        public int TileX { get; }
        public int TileY { get; }

        public TileChangedEventArgs(int tileX, int tileY)
        {
            TileX = tileX;
            TileY = tileY;
        }
    }

    /// <summary>
    /// Types of grid changes
    /// </summary>
    public enum GridChangeType
    {
        FullReset,
        CellUpdate,
        FloodFill,
        RegionUpdate
    }
}