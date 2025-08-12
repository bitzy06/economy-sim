# Country Border Detection Feature

## Overview
The country border detection feature allows users to identify countries on the political map by clicking on them. When a user right-clicks on a country, the system will detect which country is at that location and display the country's name and code.

## How to Use

1. **Switch to Political View**: Click the "Political View" button in the game interface to switch from terrain view to political view.

2. **Detect Countries**: Once in political view, RIGHT-CLICK on any country on the map to identify it.

3. **View Results**: The detected country name will appear in:
   - The window title (e.g., "Economy Sim - United States (USA)")
   - Debug output logs
   - Console output (for developers)

## Technical Implementation

### Key Components

1. **Mouse Event Handling** (`GameView.axaml.cs`)
   - Added right-click detection in `OnPointerPressed`
   - Added `DetectCountryAtPosition` method for country detection
   - Added visual feedback through window title updates

2. **Coordinate Transformation** (`HybridMapManager.cs`)
   - Added `GetCountryAtPixel` method to convert screen coordinates to geographic coordinates
   - Accounts for current zoom level and view offset (panning)
   - Performs bounds checking to ensure clicks are within the map area

3. **Geographic Point-in-Polygon Testing** (`PoliticalTileManager.cs`)
   - Added `GetCountryAtGeographicPoint` method to find countries at specific coordinates
   - Uses spatial indexing for efficient country lookup
   - Performs point-in-polygon tests using OGR geometry operations

4. **Error Handling**
   - Comprehensive error handling at all levels
   - Graceful degradation when political data is not available
   - User-friendly messages for different scenarios (ocean, out of bounds, etc.)

### Architecture

```
Mouse Click (Screen Coordinates)
    ↓
GameView.OnPointerPressed
    ↓
HybridMapManager.GetCountryAtPixel
    ↓
Coordinate Conversion (Screen → Map → Geographic)
    ↓
PoliticalTileManager.GetCountryAtGeographicPoint
    ↓
Spatial Index Lookup + Point-in-Polygon Test
    ↓
Return Country Information (or null)
    ↓
Display Feedback to User
```

### Error Scenarios Handled

- **Invalid Coordinates**: Out of bounds clicks return null gracefully
- **Ocean Clicks**: Clicks on water areas show "No country selected"
- **Terrain View**: Country detection only works in political view mode
- **Missing Data**: Handles cases where political data files are not available
- **Geometry Errors**: Catches and logs OGR geometry operation failures

## Testing

The coordinate transformation logic has been tested with:
- Map center should convert to (0°, 0°) geographic coordinates
- Map corners should convert to (-180°, 90°) and (180°, -90°)
- Known locations (e.g., USA center at -95°, 39°) map to correct pixel positions
- View offset (panning) correctly adjusts coordinate calculations
- Round-trip conversions maintain accuracy

## Future Enhancements

Potential improvements to the country detection system:

1. **Visual Feedback**
   - Highlight the detected country's border
   - Show a tooltip near the mouse cursor
   - Add a dedicated country information panel

2. **Enhanced Information**
   - Display additional country data (population, capital, etc.)
   - Show country flag
   - Display economic information

3. **User Interaction**
   - Allow selection persistence
   - Support multiple country selection
   - Add keyboard shortcuts for country detection

4. **Performance Optimizations**
   - Cache frequently accessed countries
   - Pre-load spatial index data
   - Optimize point-in-polygon calculations

## Dependencies

- **OSGeo.OGR**: Used for geometry operations and point-in-polygon tests
- **SkiaSharp**: Coordinate types (SKPointI)
- **Avalonia**: UI framework and event handling

## Files Modified

- `Views/GameView.axaml.cs`: Mouse event handling and user feedback
- `HybridMapManager.cs`: Coordinate transformation and country lookup
- `PoliticalTileManager.cs`: Geographic point-in-polygon testing
- `MultiResolutionMapManager.cs`: Added BaseWidth/BaseHeight properties