using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace Economy_sim
{
    /// <summary>
    /// Manages Level of Detail (LOD) generation from the authoritative grid using majority downsampling
    /// </summary>
    public class LodManager : IDisposable
    {
        private readonly string _dataDirectory;
        private readonly AuthoritativeGridManager _authoritativeGrid;
        private readonly ConcurrentQueue<LodRebuildJob> _rebuildQueue = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly Task _workerTask;
        
        // LOD levels (zoom levels 1-10 corresponding to PixelsPerCellLevels)
        private readonly int[] _lodLevels = MultiResolutionMapManager.PixelsPerCellLevels;
        
        public LodManager(string dataDirectory, AuthoritativeGridManager authoritativeGrid)
        {
            _dataDirectory = dataDirectory;
            _authoritativeGrid = authoritativeGrid;
            
            // Start background worker for processing rebuild jobs
            _workerTask = Task.Run(ProcessRebuildQueueAsync, _cancellationTokenSource.Token);
        }
        
        /// <summary>
        /// Enqueues LOD rebuilds for affected tiles
        /// </summary>
        public async Task EnqueueRebuildAsync(HashSet<(int x, int y)> affectedAuthTiles)
        {
            foreach (var authTile in affectedAuthTiles)
            {
                // For each LOD level, calculate which tiles need rebuilding
                for (int lodIndex = 0; lodIndex < _lodLevels.Length; lodIndex++)
                {
                    int lodLevel = lodIndex + 1; // LOD levels are 1-based
                    var lodTiles = CalculateLodTilesFromAuth(authTile, lodLevel);
                    
                    foreach (var lodTile in lodTiles)
                    {
                        _rebuildQueue.Enqueue(new LodRebuildJob
                        {
                            LodLevel = lodLevel,
                            TileX = lodTile.x,
                            TileY = lodTile.y,
                            Priority = lodLevel // Higher LOD levels (coarser) have higher priority
                        });
                    }
                }
            }
        }
        
        /// <summary>
        /// Calculates which LOD tiles are affected by an authoritative tile
        /// </summary>
        private HashSet<(int x, int y)> CalculateLodTilesFromAuth((int x, int y) authTile, int lodLevel)
        {
            var lodTiles = new HashSet<(int x, int y)>();
            
            // Calculate scaling factor from auth to LOD
            int cellSize = _lodLevels[lodLevel - 1];
            double scaleFactor = AuthoritativeGridManager.AuthoritativeWidth / (4096.0 * cellSize);
            
            // Map authoritative tile to LOD coordinate space
            int lodTileX = (int)(authTile.x / scaleFactor);
            int lodTileY = (int)(authTile.y / scaleFactor);
            
            // Add neighboring tiles that might be affected
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int tx = lodTileX + dx;
                    int ty = lodTileY + dy;
                    if (tx >= 0 && ty >= 0)
                    {
                        lodTiles.Add((tx, ty));
                    }
                }
            }
            
            return lodTiles;
        }
        
        /// <summary>
        /// Background worker that processes rebuild jobs
        /// </summary>
        private async Task ProcessRebuildQueueAsync()
        {
            var processedJobs = new HashSet<(int lodLevel, int tileX, int tileY)>();
            
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    if (_rebuildQueue.TryDequeue(out var job))
                    {
                        var jobKey = (job.LodLevel, job.TileX, job.TileY);
                        
                        // Avoid duplicate work
                        if (processedJobs.Contains(jobKey))
                            continue;
                            
                        await RebuildLodTileAsync(job);
                        processedJobs.Add(jobKey);
                        
                        // Clear processed jobs periodically to avoid memory buildup
                        if (processedJobs.Count > 10000)
                            processedJobs.Clear();
                    }
                    else
                    {
                        // No jobs available, wait a bit
                        await Task.Delay(100, _cancellationTokenSource.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in LOD rebuild worker: {ex.Message}");
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }
        }
        
        /// <summary>
        /// Rebuilds a single LOD tile using majority downsampling
        /// </summary>
        private async Task RebuildLodTileAsync(LodRebuildJob job)
        {
            try
            {
                int cellSize = _lodLevels[job.LodLevel - 1];
                
                // Calculate which authoritative tiles contribute to this LOD tile
                var authTiles = GetContributingAuthTiles(job.LodLevel, job.TileX, job.TileY);
                
                // Create the downsampled tile
                var lodTile = new uint[AuthoritativeGridManager.TileSize, AuthoritativeGridManager.TileSize];
                
                for (int y = 0; y < AuthoritativeGridManager.TileSize; y++)
                {
                    for (int x = 0; x < AuthoritativeGridManager.TileSize; x++)
                    {
                        // Apply majority downsampling
                        var authSamples = await GetAuthoritativeSamples(job.LodLevel, job.TileX, job.TileY, x, y);
                        uint majorityValue = CalculateMajorityValue(authSamples);
                        lodTile[x, y] = majorityValue;
                    }
                }
                
                // Save the LOD tile
                await SaveLodTileAsync(job.LodLevel, job.TileX, job.TileY, lodTile);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error rebuilding LOD tile {job.LodLevel}/{job.TileX}_{job.TileY}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the authoritative samples that contribute to a LOD pixel
        /// </summary>
        private async Task<List<uint>> GetAuthoritativeSamples(int lodLevel, int lodTileX, int lodTileY, int lodPixelX, int lodPixelY)
        {
            var samples = new List<uint>();
            
            // Calculate the sampling region in authoritative space
            double scaleFactor = AuthoritativeGridManager.AuthoritativeWidth / (4096.0 * _lodLevels[lodLevel - 1]);
            
            int authStartX = (int)((lodTileX * AuthoritativeGridManager.TileSize + lodPixelX) * scaleFactor);
            int authStartY = (int)((lodTileY * AuthoritativeGridManager.TileSize + lodPixelY) * scaleFactor);
            int authEndX = (int)((lodTileX * AuthoritativeGridManager.TileSize + lodPixelX + 1) * scaleFactor);
            int authEndY = (int)((lodTileY * AuthoritativeGridManager.TileSize + lodPixelY + 1) * scaleFactor);
            
            // Sample from authoritative tiles
            for (int authY = authStartY; authY < authEndY; authY++)
            {
                for (int authX = authStartX; authX < authEndX; authX++)
                {
                    if (authX >= 0 && authX < AuthoritativeGridManager.AuthoritativeWidth &&
                        authY >= 0 && authY < AuthoritativeGridManager.AuthoritativeHeight)
                    {
                        int authTileX = authX / AuthoritativeGridManager.TileSize;
                        int authTileY = authY / AuthoritativeGridManager.TileSize;
                        int localX = authX % AuthoritativeGridManager.TileSize;
                        int localY = authY % AuthoritativeGridManager.TileSize;
                        
                        try
                        {
                            var authTile = await _authoritativeGrid.GetTileForLodAsync((authTileX, authTileY));
                            if (localX >= 0 && localX < AuthoritativeGridManager.TileSize &&
                                localY >= 0 && localY < AuthoritativeGridManager.TileSize)
                            {
                                samples.Add(authTile[localX, localY]);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error sampling auth tile {authTileX},{authTileY}: {ex.Message}");
                        }
                    }
                }
            }
            
            return samples;
        }
        
        /// <summary>
        /// Calculates the majority value from samples using the algorithm from the problem statement
        /// </summary>
        private uint CalculateMajorityValue(List<uint> samples)
        {
            if (samples.Count == 0) return 0;
            if (samples.Count == 1) return samples[0];
            
            // Fast-path: if all samples are equal
            uint first = samples[0];
            bool allEqual = true;
            foreach (var sample in samples)
            {
                if (sample != first)
                {
                    allEqual = false;
                    break;
                }
            }
            if (allEqual) return first;
            
            // Count votes
            var votes = new Dictionary<uint, int>();
            foreach (var sample in samples)
            {
                votes[sample] = votes.GetValueOrDefault(sample, 0) + 1;
            }
            
            // Find majority, prefer first value on tie
            uint winner = first;
            int maxVotes = votes[first];
            
            foreach (var kvp in votes)
            {
                if (kvp.Value > maxVotes)
                {
                    winner = kvp.Key;
                    maxVotes = kvp.Value;
                }
            }
            
            return winner;
        }
        
        /// <summary>
        /// Gets the list of authoritative tiles that contribute to a LOD tile
        /// </summary>
        private List<(int x, int y)> GetContributingAuthTiles(int lodLevel, int lodTileX, int lodTileY)
        {
            var authTiles = new List<(int x, int y)>();
            
            double scaleFactor = AuthoritativeGridManager.AuthoritativeWidth / (4096.0 * _lodLevels[lodLevel - 1]);
            
            int authStartX = (int)(lodTileX * AuthoritativeGridManager.TileSize * scaleFactor);
            int authStartY = (int)(lodTileY * AuthoritativeGridManager.TileSize * scaleFactor);
            int authEndX = (int)((lodTileX + 1) * AuthoritativeGridManager.TileSize * scaleFactor);
            int authEndY = (int)((lodTileY + 1) * AuthoritativeGridManager.TileSize * scaleFactor);
            
            int startTileX = authStartX / AuthoritativeGridManager.TileSize;
            int endTileX = (authEndX + AuthoritativeGridManager.TileSize - 1) / AuthoritativeGridManager.TileSize;
            int startTileY = authStartY / AuthoritativeGridManager.TileSize;
            int endTileY = (authEndY + AuthoritativeGridManager.TileSize - 1) / AuthoritativeGridManager.TileSize;
            
            for (int ty = startTileY; ty < endTileY; ty++)
            {
                for (int tx = startTileX; tx < endTileX; tx++)
                {
                    authTiles.Add((tx, ty));
                }
            }
            
            return authTiles;
        }
        
        /// <summary>
        /// Saves a LOD tile to disk
        /// </summary>
        private async Task SaveLodTileAsync(int lodLevel, int tileX, int tileY, uint[,] tile)
        {
            string lodDir = Path.Combine(_dataDirectory, "grids", $"lod{lodLevel}");
            Directory.CreateDirectory(lodDir);
            
            string filePath = Path.Combine(lodDir, $"{tileX}_{tileY}.bin");
            
            try
            {
                var data = new byte[AuthoritativeGridManager.TileSize * AuthoritativeGridManager.TileSize * sizeof(uint)];
                Buffer.BlockCopy(tile, 0, data, 0, data.Length);
                
                await File.WriteAllBytesAsync(filePath, data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving LOD tile {lodLevel}/{tileX}_{tileY}: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            try
            {
                _workerTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing LOD manager: {ex.Message}");
            }
            _cancellationTokenSource.Dispose();
        }
        
        /// <summary>
        /// Represents a LOD rebuild job
        /// </summary>
        private class LodRebuildJob
        {
            public int LodLevel { get; set; }
            public int TileX { get; set; }
            public int TileY { get; set; }
            public int Priority { get; set; }
        }
    }
}