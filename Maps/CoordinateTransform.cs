using System;
using SkiaSharp;

namespace Economy_sim
{
    /// <summary>
    /// Unified coordinate transformation utilities to ensure consistency between terrain and political rendering
    /// </summary>
    public static class CoordinateTransform
    {
        // World geographic bounds (standard Web Mercator-like projection)
        public const double WORLD_MIN_LON = -180.0;
        public const double WORLD_MAX_LON = 180.0;
        public const double WORLD_MIN_LAT = -90.0;
        public const double WORLD_MAX_LAT = 90.0;
        public const double WORLD_WIDTH_DEG = 360.0;
        public const double WORLD_HEIGHT_DEG = 180.0;

        /// <summary>
        /// Convert geographic coordinates to pixel coordinates for a given map size
        /// </summary>
        public static SKPointI GeographicToPixel(double longitude, double latitude, int mapWidth, int mapHeight)
        {
            // Normalize to 0-1 range
            double normalizedX = (longitude - WORLD_MIN_LON) / WORLD_WIDTH_DEG;
            double normalizedY = (WORLD_MAX_LAT - latitude) / WORLD_HEIGHT_DEG; // Flip Y axis
            
            // Convert to pixel coordinates
            int pixelX = (int)Math.Round(normalizedX * mapWidth);
            int pixelY = (int)Math.Round(normalizedY * mapHeight);
            
            // Clamp to valid range
            pixelX = Math.Clamp(pixelX, 0, mapWidth - 1);
            pixelY = Math.Clamp(pixelY, 0, mapHeight - 1);
            
            return new SKPointI(pixelX, pixelY);
        }

        /// <summary>
        /// Convert pixel coordinates to geographic coordinates for a given map size
        /// </summary>
        public static (double longitude, double latitude) PixelToGeographic(int pixelX, int pixelY, int mapWidth, int mapHeight)
        {
            // Normalize to 0-1 range
            double normalizedX = (double)pixelX / mapWidth;
            double normalizedY = (double)pixelY / mapHeight;
            
            // Convert to geographic coordinates
            double longitude = WORLD_MIN_LON + (normalizedX * WORLD_WIDTH_DEG);
            double latitude = WORLD_MAX_LAT - (normalizedY * WORLD_HEIGHT_DEG); // Flip Y axis
            
            // Clamp to valid range
            longitude = Math.Clamp(longitude, WORLD_MIN_LON, WORLD_MAX_LON);
            latitude = Math.Clamp(latitude, WORLD_MIN_LAT, WORLD_MAX_LAT);
            
            return (longitude, latitude);
        }

        /// <summary>
        /// Calculate geographic bounds for a pixel tile
        /// </summary>
        public static GeoBounds GetTileGeographicBounds(int tileX, int tileY, int tileSizePx, int mapWidth, int mapHeight)
        {
            int pixelX = tileX * tileSizePx;
            int pixelY = tileY * tileSizePx;
            int tileWidth = Math.Min(tileSizePx, mapWidth - pixelX);
            int tileHeight = Math.Min(tileSizePx, mapHeight - pixelY);
            
            var topLeft = PixelToGeographic(pixelX, pixelY, mapWidth, mapHeight);
            var bottomRight = PixelToGeographic(pixelX + tileWidth, pixelY + tileHeight, mapWidth, mapHeight);
            
            return new GeoBounds
            {
                MinLon = topLeft.longitude,
                MaxLon = bottomRight.longitude,
                MinLat = bottomRight.latitude, // Note: bottom-right has lower latitude
                MaxLat = topLeft.latitude      // Note: top-left has higher latitude
            };
        }

        /// <summary>
        /// Validate that geographic bounds are reasonable
        /// </summary>
        public static bool IsValidGeoBounds(GeoBounds bounds)
        {
            return bounds.MinLon >= WORLD_MIN_LON && bounds.MaxLon <= WORLD_MAX_LON &&
                   bounds.MinLat >= WORLD_MIN_LAT && bounds.MaxLat <= WORLD_MAX_LAT &&
                   bounds.MinLon < bounds.MaxLon && bounds.MinLat < bounds.MaxLat;
        }

        /// <summary>
        /// Convert geographic coordinates to grid cell coordinates
        /// </summary>
        public static (int cellX, int cellY) GeographicToGridCell(double longitude, double latitude, int gridWidth, int gridHeight)
        {
            // Normalize to 0-1 range
            double normalizedX = (longitude - WORLD_MIN_LON) / WORLD_WIDTH_DEG;
            double normalizedY = (WORLD_MAX_LAT - latitude) / WORLD_HEIGHT_DEG; // Flip Y axis

            // Convert to grid cell coordinates
            int cellX = (int)Math.Floor(normalizedX * gridWidth);
            int cellY = (int)Math.Floor(normalizedY * gridHeight);

            // Clamp to valid range
            cellX = Math.Clamp(cellX, 0, gridWidth - 1);
            cellY = Math.Clamp(cellY, 0, gridHeight - 1);

            return (cellX, cellY);
        }

        /// <summary>
        /// Convert grid cell coordinates to geographic coordinates (cell center)
        /// </summary>
        public static (double longitude, double latitude) GridCellToGeographic(int cellX, int cellY, int gridWidth, int gridHeight)
        {
            // Add 0.5 to get cell center
            double normalizedX = (cellX + 0.5) / gridWidth;
            double normalizedY = (cellY + 0.5) / gridHeight;

            // Convert to geographic coordinates
            double longitude = WORLD_MIN_LON + (normalizedX * WORLD_WIDTH_DEG);
            double latitude = WORLD_MAX_LAT - (normalizedY * WORLD_HEIGHT_DEG); // Flip Y axis

            // Clamp to valid range
            longitude = Math.Clamp(longitude, WORLD_MIN_LON, WORLD_MAX_LON);
            latitude = Math.Clamp(latitude, WORLD_MIN_LAT, WORLD_MAX_LAT);

            return (longitude, latitude);
        }

        /// <summary>
        /// Calculate geographic bounds for a grid tile
        /// </summary>
        public static GeoBounds GetGridTileGeographicBounds(int tileX, int tileY, int tileSize, int gridWidth, int gridHeight)
        {
            int cellX = tileX * tileSize;
            int cellY = tileY * tileSize;
            int tileWidth = Math.Min(tileSize, gridWidth - cellX);
            int tileHeight = Math.Min(tileSize, gridHeight - cellY);

            var topLeft = GridCellToGeographic(cellX, cellY, gridWidth, gridHeight);
            var bottomRight = GridCellToGeographic(cellX + tileWidth - 1, cellY + tileHeight - 1, gridWidth, gridHeight);

            return new GeoBounds
            {
                MinLon = topLeft.longitude - (topLeft.longitude - bottomRight.longitude) / (2 * tileWidth),
                MaxLon = bottomRight.longitude + (topLeft.longitude - bottomRight.longitude) / (2 * tileWidth),
                MinLat = bottomRight.latitude - (topLeft.latitude - bottomRight.latitude) / (2 * tileHeight),
                MaxLat = topLeft.latitude + (topLeft.latitude - bottomRight.latitude) / (2 * tileHeight)
            };
        }
    }

    /// <summary>
    /// Geographic bounds structure used across the system
    /// </summary>
    public struct GeoBounds
    {
        public double MinLon { get; set; }
        public double MaxLon { get; set; }
        public double MinLat { get; set; }
        public double MaxLat { get; set; }

        public double Width => MaxLon - MinLon;
        public double Height => MaxLat - MinLat;

        public override string ToString()
        {
            return $"GeoBounds(Lon:[{MinLon:F4}, {MaxLon:F4}], Lat:[{MinLat:F4}, {MaxLat:F4}])";
        }
    }
}