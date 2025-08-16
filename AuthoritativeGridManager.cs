using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SkiaSharp;
using System.Drawing;
using System.Collections.Concurrent;
using System.Threading;

namespace Economy_sim
{
    /// <summary>
    /// Manages the authoritative highest-resolution grid (16384x8192) and generates LODs via downsampling.
    /// This is the only grid that can be written to directly - all edits end up here.
    /// </summary>
    public class AuthoritativeGridManager : IDisposable
    {
        // Authoritative grid dimensions
        public const int AuthoritativeWidth = 16384;
        public const int AuthoritativeHeight = 8192;
        public const int TileSize = 512;
        
        private readonly string _baseGridPath;
        private readonly EditDeltaManager _deltaManager;
        private readonly LodManager _lodManager;
        
        // Tile-based storage for the authoritative grid
        private readonly Dictionary<(int x, int y), uint[,]> _loadedTiles = new();
        private readonly object _tileLock = new();

        // Per-file IO locks to serialize access to tile files
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();
        private static SemaphoreSlim GetFileLock(string path) => _fileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        
        public AuthoritativeGridManager(string dataDirectory)
        {
            _baseGridPath = Path.Combine(dataDirectory, "grids", "lod0");
            _deltaManager = new EditDeltaManager(Path.Combine(dataDirectory, "edits"));
            _lodManager = new LodManager(dataDirectory, this);
            
            Directory.CreateDirectory(_baseGridPath);
        }
        
        /// <summary>
        /// Maps an edit from any zoom level to the authoritative grid
        /// </summary>
        public async Task ApplyEditAsync(int sourceZoomLevel, Rectangle editRegion, uint newValue, EditPolicy policy = EditPolicy.FillAllSubcells)
        {
            // Convert edit region from source zoom to authoritative grid coordinates
            var authoritativeRegion = MapRegionToAuthoritative(sourceZoomLevel, editRegion);
            
            // Track the edit for undo/redo
            var changes = new List<(Point cell, uint previousValue)>();
            
            // Apply the edit to affected tiles
            var affectedTiles = GetAffectedTiles(authoritativeRegion);
            
            foreach (var tileKey in affectedTiles)
            {
                var tile = await GetOrLoadTileAsync(tileKey).ConfigureAwait(false);
                var tileRegion = GetTileRegion(authoritativeRegion, tileKey);
                
                foreach (var point in EnumeratePoints(tileRegion))
                {
                    var localX = point.X % TileSize;
                    var localY = point.Y % TileSize;
                    
                    if (localX >= 0 && localX < TileSize && localY >= 0 && localY < TileSize)
                    {
                        uint previousValue = tile[localX, localY];
                        
                        if (policy == EditPolicy.BorderAware)
                        {
                            // TODO: Check border mask/SDF to respect coastlines/political boundaries
                            // For now, just apply the edit
                        }
                        
                        tile[localX, localY] = newValue;
                        changes.Add((point, previousValue));
                    }
                }
                
                // Mark tile as dirty for saving
                await SaveTileAsync(tileKey, tile).ConfigureAwait(false);
            }
            
            // Record the edit in the delta log
            _deltaManager.RecordEdit(new EditOperation
            {
                Region = authoritativeRegion,
                NewValue = newValue,
                Changes = changes,
                Timestamp = DateTime.UtcNow,
                Policy = policy
            });
            
            // Trigger LOD rebuilds for affected areas
            await _lodManager.EnqueueRebuildAsync(affectedTiles).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Maps a region from source zoom level to authoritative grid coordinates
        /// </summary>
        private Rectangle MapRegionToAuthoritative(int sourceZoomLevel, Rectangle sourceRegion)
        {
            int sourceCellSize = MultiResolutionMapManager.PixelsPerCellLevels[Math.Clamp(sourceZoomLevel - 1, 0, MultiResolutionMapManager.PixelsPerCellLevels.Length - 1)];
            
            // Calculate the scaling factor from source to authoritative
            // Authoritative grid assumes 1 pixel per cell, so we need to scale up
            double scaleFactor = AuthoritativeWidth / 4096.0; // Base map is 4096x2048
            
            int authX = (int)(sourceRegion.X * scaleFactor);
            int authY = (int)(sourceRegion.Y * scaleFactor);
            int authWidth = (int)(sourceRegion.Width * scaleFactor);
            int authHeight = (int)(sourceRegion.Height * scaleFactor);
            
            return new Rectangle(authX, authY, authWidth, authHeight);
        }
        
        /// <summary>
        /// Gets all tile keys affected by a region
        /// </summary>
        private HashSet<(int x, int y)> GetAffectedTiles(Rectangle region)
        {
            var tiles = new HashSet<(int x, int y)>();
            
            int startTileX = region.Left / TileSize;
            int endTileX = (region.Right + TileSize - 1) / TileSize;
            int startTileY = region.Top / TileSize;
            int endTileY = (region.Bottom + TileSize - 1) / TileSize;
            
            for (int tx = startTileX; tx < endTileX; tx++)
            {
                for (int ty = startTileY; ty < endTileY; ty++)
                {
                    tiles.Add((tx, ty));
                }
            }
            
            return tiles;
        }
        
        /// <summary>
        /// Gets the intersection of a region with a specific tile
        /// </summary>
        private Rectangle GetTileRegion(Rectangle region, (int x, int y) tileKey)
        {
            var tileRect = new Rectangle(tileKey.x * TileSize, tileKey.y * TileSize, TileSize, TileSize);
            return Rectangle.Intersect(region, tileRect);
        }
        
        /// <summary>
        /// Enumerates all points in a rectangle
        /// </summary>
        private IEnumerable<Point> EnumeratePoints(Rectangle region)
        {
            for (int y = region.Top; y < region.Bottom; y++)
            {
                for (int x = region.Left; x < region.Right; x++)
                {
                    yield return new Point(x, y);
                }
            }
        }
        
        /// <summary>
        /// Gets or loads a tile from disk
        /// </summary>
        private async Task<uint[,]> GetOrLoadTileAsync((int x, int y) tileKey)
        {
            lock (_tileLock)
            {
                if (_loadedTiles.TryGetValue(tileKey, out var cachedTile))
                {
                    return cachedTile;
                }
            }
            
            var tile = await LoadTileAsync(tileKey).ConfigureAwait(false);
            
            lock (_tileLock)
            {
                _loadedTiles[tileKey] = tile;
            }
            
            return tile;
        }
        
        /// <summary>
        /// Loads a tile from disk or creates a new empty one. Uses a shared per-file lock to avoid
        /// concurrent write/read collisions with SaveTileAsync.
        /// </summary>
        private async Task<uint[,]> LoadTileAsync((int x, int y) tileKey)
        {
            string filePath = Path.Combine(_baseGridPath, $"{tileKey.x}_{tileKey.y}.bin");
            var sem = GetFileLock(filePath);

            if (File.Exists(filePath))
            {
                await sem.WaitAsync().ConfigureAwait(false);
                try
                {
                    // Read all at once to avoid holding the file open for long
                    var data = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
                    var tile = new uint[TileSize, TileSize];
                    Buffer.BlockCopy(data, 0, tile, 0, Math.Min(data.Length, TileSize * TileSize * sizeof(uint)));
                    return tile;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading tile {tileKey}: {ex.Message}");
                }
                finally
                {
                    sem.Release();
                }
            }
            
            // Return empty tile (default to water/unassigned)
            return new uint[TileSize, TileSize];
        }
        
        /// <summary>
        /// Saves a tile to disk using atomic write (temp file + replace) and a per-file semaphore
        /// to prevent 'file in use' errors when multiple edits touch the same tile rapidly.
        /// </summary>
        private async Task SaveTileAsync((int x, int y) tileKey, uint[,] tile)
        {
            string dir = _baseGridPath;
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, $"{tileKey.x}_{tileKey.y}.bin");
            string tempPath = filePath + ".tmp";
            var sem = GetFileLock(filePath);

            await sem.WaitAsync().ConfigureAwait(false);
            try
            {
                var data = new byte[TileSize * TileSize * sizeof(uint)];
                Buffer.BlockCopy(tile, 0, data, 0, data.Length);

                // Write to temp file first
                await File.WriteAllBytesAsync(tempPath, data).ConfigureAwait(false);

                // Atomically replace destination
#if NET8_0_OR_GREATER
                File.Move(tempPath, filePath, overwrite: true);
#else
                if (File.Exists(filePath)) File.Delete(filePath);
                File.Move(tempPath, filePath);
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving tile {tileKey}: {ex.Message}");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            }
            finally
            {
                sem.Release();
            }
        }
        
        /// <summary>
        /// Gets a tile for LOD generation
        /// </summary>
        public async Task<uint[,]> GetTileForLodAsync((int x, int y) tileKey)
        {
            return await GetOrLoadTileAsync(tileKey).ConfigureAwait(false);
        }
        
        /// <summary>
        /// Undoes the last edit operation
        /// </summary>
        public async Task<bool> UndoAsync()
        {
            var operation = _deltaManager.PopLastEdit();
            if (operation == null) return false;
            
            // Restore the previous values
            var affectedTiles = GetAffectedTiles(operation.Region);
            
            foreach (var (cell, previousValue) in operation.Changes)
            {
                var tileKey = (cell.X / TileSize, cell.Y / TileSize);
                var tile = await GetOrLoadTileAsync(tileKey).ConfigureAwait(false);
                
                int localX = cell.X % TileSize;
                int localY = cell.Y % TileSize;
                
                if (localX >= 0 && localX < TileSize && localY >= 0 && localY < TileSize)
                {
                    tile[localX, localY] = previousValue;
                }
            }
            
            // Save affected tiles
            foreach (var tileKey in affectedTiles)
            {
                if (_loadedTiles.TryGetValue(tileKey, out var tile))
                {
                    await SaveTileAsync(tileKey, tile).ConfigureAwait(false);
                }
            }
            
            // Trigger LOD rebuilds
            await _lodManager.EnqueueRebuildAsync(affectedTiles).ConfigureAwait(false);
            
            return true;
        }
        
        /// <summary>
        /// Redoes the next edit operation
        /// </summary>
        public async Task<bool> RedoAsync()
        {
            var operation = _deltaManager.GetNextRedo();
            if (operation == null) return false;
            
            // Re-apply the edit
            await ApplyEditAsync(1, operation.Region, operation.NewValue, operation.Policy).ConfigureAwait(false);
            
            return true;
        }
        
        public void Dispose()
        {
            _deltaManager?.Dispose();
            _lodManager?.Dispose();
        }
    }
    
    /// <summary>
    /// Edit policy for how to apply edits
    /// </summary>
    public enum EditPolicy
    {
        FillAllSubcells,    // Safe: fill all N×N subcells
        BorderAware         // Pretty: clip against border mask
    }
    
    /// <summary>
    /// Represents a single edit operation
    /// </summary>
    public class EditOperation
    {
        public Rectangle Region { get; set; }
        public uint NewValue { get; set; }
        public List<(Point cell, uint previousValue)> Changes { get; set; } = new();
        public DateTime Timestamp { get; set; }
        public EditPolicy Policy { get; set; }
    }
}