# Political Borders Feature

This document describes the political borders functionality implemented in the Economy Simulator.

## Overview

The political borders feature allows users to switch between terrain and political map views in the game. It supports temporal mapping using CShapes-2.0.shp data, with the ability to display country boundaries for different time periods (defaulting to January 1950).

## Features

### Map View Toggle
- **Terrain View**: Shows the standard terrain map
- **Political View**: Shows country boundaries with color-coded political entities
- Toggle buttons are located in the game UI's bottom control bar

### CShapes Integration
- Uses CShapes-2.0.shp data for historical political boundaries
- Filters data by date (default: January 1950)
- Supports temporal queries to show boundaries as they existed at specific times

### Color Management
- Automatic random color assignment for countries
- Color mapping stored in `data/country_borders/country_colors.json`
- Persistent color assignments across sessions
- Checks for existing color file before creating new assignments

### Memory Efficiency
- Separate layer system that doesn't overlay on terrain tiles
- Memory-efficient caching with size limits
- Automatic cache cleanup to prevent RAM explosion
- Political maps are generated and cached separately from terrain

## File Structure

```
data/
└── country_borders/
    ├── CShapes-2.0.shp          # Place CShapes data here
    ├── CShapes-2.0.shx          # Supporting files
    ├── CShapes-2.0.dbf          # 
    ├── CShapes-2.0.prj          # 
    └── country_colors.json      # Auto-generated color mapping
```

## Usage Instructions

### Setup
1. Download CShapes-2.0.shp data from the appropriate source
2. Place all CShapes files in `data/country_borders/` directory
3. The system will automatically detect and use the files

### In-Game Controls
1. Start the game normally
2. In the GameView, look for the "Terrain" and "Political" buttons
3. Click "Political" to switch to political borders view
4. Click "Terrain" to switch back to terrain view
5. The current view type is indicated by button highlighting

### Without CShapes Data
- The system gracefully handles missing CShapes files
- Political view will show a notification that data is unavailable
- All other functionality remains intact

## Technical Implementation

### Key Classes
- **PoliticalBorderManager**: Handles CShapes data processing and color management
- **HybridMapManager**: Manages switching between terrain and political views
- **MapViewType**: Enum defining available view types

### Data Processing
- Uses GDAL/OGR for shapefile processing
- Filters features by start/end date fields (GWSYEAR/GWEYER)
- Rasterizes political boundaries to create displayable maps
- Generates country masks with unique integer codes

### Performance Optimizations
- LRU-style cache management (max 5 cached views)
- Separate memory layer prevents terrain cache interference
- Background processing for map generation
- Efficient bitmap rendering using SkiaSharp

## Configuration

### Default Settings
- **Target Date**: January 1, 1950
- **Color File**: `data/country_borders/country_colors.json`
- **Cache Size**: 5 political map views
- **Default Resolution**: Matches terrain map resolution

### Customization
The political map date can be changed programmatically:
```csharp
hybridManager.SetPoliticalMapDate(new DateTime(1960, 1, 1));
```

Color assignments can be modified by editing the JSON file directly or through the API:
```csharp
var color = politicalManager.GetCountryColor("USA");
politicalManager.SaveColorMapping();
```

## Error Handling

- **Missing CShapes Files**: System logs warning and disables political view
- **Invalid Date Ranges**: Falls back to available data closest to target date
- **Corrupt Data**: GDAL exceptions are caught and logged
- **Memory Issues**: Cache limits prevent excessive RAM usage

## Dependencies

- **GDAL/OGR**: Geospatial data processing
- **SkiaSharp**: Bitmap rendering and graphics
- **System.Text.Json**: Color mapping serialization
- **Avalonia**: UI framework integration

## Future Enhancements

Potential improvements that could be added:
- User-selectable time periods via UI controls
- Country-specific color customization interface
- Political entity information display on hover
- Export functionality for political maps
- Support for other shapefile formats beyond CShapes