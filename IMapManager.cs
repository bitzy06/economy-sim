using System;
using SkiaSharp;

namespace StrategyGame
{
    /// <summary>
    /// Common interface for map managers to enable switching between raster and vector rendering
    /// </summary>
    public interface IMapManager : IDisposable
    {
        /// <summary>
        /// Current map view type (Terrain or Political)
        /// </summary>
        MapViewType CurrentViewType { get; }
        
        /// <summary>
        /// Current political map date
        /// </summary>
        DateTime PoliticalMapDate { get; }
        
        /// <summary>
        /// Event raised when view type changes
        /// </summary>
        event EventHandler<MapViewType>? ViewTypeChanged;
        
        /// <summary>
        /// Set the current view type
        /// </summary>
        void SetViewType(MapViewType viewType);
        
        /// <summary>
        /// Set the political map date
        /// </summary>
        void SetPoliticalMapDate(DateTime date);
        
        /// <summary>
        /// Assemble a view of the map for the specified parameters
        /// </summary>
        SKBitmap? AssembleView(int zoomLevel, SKRectI viewArea, Action? onTileReady = null);
        
        /// <summary>
        /// Get the map size for the specified zoom level
        /// </summary>
        SKSizeI GetMapSize(int zoomLevel);
        
        /// <summary>
        /// Get the cell size for the specified zoom level
        /// </summary>
        int GetCellSizeForZoom(int zoomLevel);
    }
}