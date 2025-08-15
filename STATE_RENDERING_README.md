# State-Level Rendering Feature

This document describes the state/province rendering functionality implemented in the Economy Simulator, extending the existing political borders system.

## Overview

The state-level rendering feature adds support for displaying administrative subdivisions (states, provinces, etc.) using Natural Earth's `ne_10m_admin_1_states_provinces` dataset. States are rendered as overlays on the political map at higher zoom levels for enhanced geographic detail.

## Features

### Zoom-Based State Rendering
- **Threshold-based display**: States are only rendered when zoom level is 3 or higher
- **Performance optimized**: Avoids rendering complexity at lower zoom levels where states would be too small to see
- **Seamless integration**: Works seamlessly with existing country borders

### State Selection and Highlighting
- **Country-first selection**: When a country is selected, only states within that country are available for selection
- **Visual feedback**: Selected states are highlighted with yellow borders
- **Other state borders**: Non-selected states within the selected country get gray borders
- **Interactive selection**: Left-click selects states (at high zoom) or countries (at lower zoom)
- **Detection mode**: Right-click identifies states and countries without selecting them

### Data Integration
- **Natural Earth dataset**: Uses `ne_10m_admin_1_states_provinces.shp` for accurate state boundaries
- **Field mapping**: Supports standard Natural Earth fields (`ISO_A2`, `ADM0_A3` for countries, `NAME`/`ADM1_NAME` for states)
- **Color management**: Automatic color generation and caching for consistent state appearance
- **Persistent storage**: State colors are cached in `data/country_borders/states_colors.json`

## File Structure

Place the Natural Earth states dataset in the following location:

```
Documents/
└── data/
    └── country_borders/
        └── states/
            ├── ne_10m_admin_1_states_provinces.shp   # Main shapefile
            ├── ne_10m_admin_1_states_provinces.shx   # Spatial index
            ├── ne_10m_admin_1_states_provinces.dbf   # Attribute data
            └── ne_10m_admin_1_states_provinces.prj   # Projection info
```

The system will automatically create:
```
Documents/
└── data/
    └── country_borders/
        └── states_colors.json   # Auto-generated state color mapping
```

## Technical Implementation

### Architecture Components

- **StatesBorderManager**: Handles state shapefile processing and mask generation
- **StatesDataCache**: Manages cached state data and color mappings for performance
- **StatesSpatialIndex**: Provides fast geographic lookups and boundary testing
- **Enhanced PoliticalTileManager**: Extended to render state overlays at appropriate zoom levels
- **Updated HybridMapManager**: Manages state selection and provides state lookup APIs

### Performance Optimizations

- **Tile-based rendering**: States are rendered as part of the existing tile system for optimal performance
- **Spatial indexing**: Fast geographic lookups using R-tree-style indexing
- **Memory management**: Efficient caching with LRU eviction and reference counting
- **Zoom-based filtering**: States only processed and rendered when zoom level is appropriate
- **Country filtering**: When a country is selected, state processing is limited to that country

### Rendering Pipeline

1. **Country rendering**: Standard country boundaries rendered first
2. **Zoom level check**: Only proceed with state rendering if zoom ≥ 3
3. **Country filter**: If a country is selected, only render states within that country
4. **State mask generation**: Create rasterized state boundaries for the tile region
5. **Border detection**: Find boundaries between different states
6. **Overlay rendering**: Draw state borders as overlays using SkiaSharp
7. **Selection highlighting**: Apply special highlighting for selected states

## Usage Instructions

### In-Game Controls

1. **Switch to Political View**: Click the "Political" button to enable political borders
2. **Zoom in**: Use mouse wheel to zoom to level 3 or higher to see states
3. **Select Country**: Left-click on a country to select it (shows white country borders)
4. **Select State**: At high zoom levels, left-click on a state within the selected country
5. **Identify Features**: Right-click to identify countries or states without selecting them
6. **Clear Selection**: Left-click on water/empty areas to clear selections

### Visual Feedback

- **Window Title**: Updates to show selected country or state information
- **Country Borders**: Selected countries show white borders
- **State Borders**: Selected states show yellow borders, other states in selected country show gray borders
- **Debug Output**: Detailed logging in Debug console shows selection and rendering information

## Configuration

### Zoom Threshold

The zoom level threshold for state rendering can be adjusted by modifying the `StateRenderingZoomThreshold` constant in `PoliticalTileManager.cs`:

```csharp
private const int StateRenderingZoomThreshold = 3; // Change this value to adjust when states appear
```

### State Colors

State colors are automatically generated based on a hash of the country code and state name, ensuring consistency across sessions. Colors are cached in JSON format and can be manually edited if desired.

### Shapefile Field Mapping

The system looks for these fields in the states shapefile (in order of preference):
- **Country Code**: `ISO_A2`, `ADM0_A3`
- **State Name**: `NAME`, `ADM1_NAME`, `NAME_EN`

## Troubleshooting

### States Not Appearing

1. **Check zoom level**: States only appear at zoom level 3 or higher
2. **Verify file location**: Ensure shapefile is in the correct `Documents/data/country_borders/states/` directory
3. **Select a country first**: States are only shown for the currently selected country
4. **Check debug output**: Look for state loading messages in the debug console

### Performance Issues

1. **Reduce zoom level**: Lower zoom levels reduce state rendering complexity
2. **Select specific countries**: State rendering is optimized when a specific country is selected
3. **Check available memory**: Large state datasets may require adequate system memory

### File Not Found Errors

1. **Verify shapefile completeness**: Ensure all files (.shp, .shx, .dbf, .prj) are present
2. **Check file permissions**: Ensure the application can read the shapefile directory
3. **Validate shapefile integrity**: Use a GIS tool to verify the shapefile is not corrupted

## Future Enhancements

Potential improvements that could be added:
- **Dynamic zoom thresholds**: Adjust state rendering based on geographic region density
- **Hierarchical selection**: Support for sub-state administrative levels (counties, municipalities)
- **State information panels**: Display detailed state statistics and information
- **Custom state styling**: User-configurable colors and border styles for states
- **State-based simulation features**: Integrate states into the economic simulation system
- **Multi-country state display**: Show states for all visible countries at high zoom levels