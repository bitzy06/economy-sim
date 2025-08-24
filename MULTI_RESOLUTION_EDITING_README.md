# Multi-Resolution Map Editing System

This document describes the new multi-resolution map editing system implemented according to the authoritative grid architecture.

## Overview

The system implements a hierarchical map editing approach with the following key principles:

1. **Single Authoritative Grid**: The highest resolution grid (16384×8192) is the only writable source of truth
2. **Read-only LODs**: All lower zoom levels are generated via majority downsampling from the authoritative grid
3. **Precise Edits**: Edits at any zoom level map to precise changes on the fine grid
4. **Sparse Delta Storage**: Edit history is stored as deltas for fast saves and undo/redo
5. **Incremental LOD Rebuilds**: Only affected tiles are regenerated when edits are made

## Architecture

### Core Components

#### AuthoritativeGridManager
- Manages the 16384×8192 authoritative grid stored as 512×512 tiles
- Handles edit operations and mapping from any zoom level to fine grid coordinates
- Provides undo/redo functionality through delta management
- File location: `AuthoritativeGridManager.cs`

#### LodManager
- Generates Level of Detail (LOD) maps using majority downsampling
- Processes rebuild jobs in the background for affected tiles
- Implements the majority voting algorithm for categorical data
- File location: `LodManager.cs`

#### EditDeltaManager
- Stores edit operations as sparse deltas for efficient storage
- Manages undo/redo stacks with configurable limits
- Serializes edit history to JSON for persistence
- File location: `EditDeltaManager.cs`

#### EnhancedMapEditor
- Provides the high-level editing interface
- Converts screen coordinates to world space for precise editing
- Shows precision indicators and dirty tile glow effects
- File location: `EnhancedMapEditor.cs`

### File Structure

The system organizes data in the following structure:

```
data/
├── grids/
│   ├── lod0/           # Authoritative grid (16384×8192)
│   │   ├── 0_0.bin     # Tile files (512×512 each)
│   │   ├── 0_1.bin
│   │   └── ...
│   ├── lod1/           # First downsampled level
│   ├── lod2/           # Second downsampled level
│   └── ...
├── borders/            # Future: Vector/SDF border overlays
│   └── vectors/
│       └── {z}/{x}/{y}.pbf
└── edits/
    └── delta.log       # Sparse edit operations
```

## Edit Policies

The system supports two edit policies:

### FillAllSubcells (Safe)
- Painting one coarse cell writes all its N×N fine subcells
- Guarantees no ambiguity and no "lost" slivers
- Default mode for predictable results

### BorderAware (Pretty)
- Same as FillAllSubcells but clips against border masks
- Respects coastlines and political boundaries without jaggies
- Requires border data to be implemented

## Usage

### Basic Editing

```csharp
// Initialize the enhanced editor
var enhancedEditor = new EnhancedMapEditor(dataDirectory, mapManager);

// Set brush properties
enhancedEditor.SetBrushValue(countryId);
enhancedEditor.SetBrushSize(3);
enhancedEditor.SetEditPolicy(EditPolicy.FillAllSubcells);

// Apply edit at screen coordinates
await enhancedEditor.ApplyEditAsync(screenX, screenY, zoomLevel, viewOffset);
```

### Undo/Redo Operations

```csharp
// Undo last edit
bool undoResult = await enhancedEditor.UndoAsync();

// Redo next edit
bool redoResult = await enhancedEditor.RedoAsync();
```

### Map Editor Integration

The existing `MapEditorWindow` has been enhanced with:

- **Ctrl+Z**: Undo last operation
- **Ctrl+Y**: Redo next operation  
- **B**: Toggle between FillAllSubcells and BorderAware policies
- **P**: Toggle precision indicator
- **G**: Toggle dirty tile glow
- **E**: Toggle enhanced editor on/off

## Technical Details

### Majority Downsampling Algorithm

The LOD generation uses majority voting with tie-breaking:

```csharp
uint DownsampleMajority(List<uint> samples)
{
    // Fast-path: if all samples equal, return that value
    if (all samples equal) return sampleValue;
    
    // Count votes, prefer first value on tie
    return mostFrequentValue ?? firstValue;
}
```

### Coordinate Mapping

Edits are mapped from source zoom level to authoritative coordinates:

1. Convert screen coordinates to world coordinates using current view offset
2. Scale world coordinates to grid coordinates based on cell size
3. Map grid coordinates to authoritative space (16384×8192)
4. Apply edit to affected authoritative tiles

### Performance Optimizations

- **Tiled Storage**: 512×512 tiles reduce memory usage and I/O
- **Background Processing**: LOD rebuilds happen asynchronously
- **Incremental Updates**: Only affected tiles are regenerated
- **LRU Caching**: Frequently used tiles stay in memory
- **Sparse Deltas**: Only changed cells are stored in edit history

## Testing

Run the test suite to verify functionality:

```bash
dotnet run --project "Economy sim.csproj" -- --test
```

This runs basic tests for:
- Edit application
- Undo/redo operations
- Multiple edit handling
- File I/O operations

## Future Enhancements

1. **Vector Border Overlays**: Implement crisp border rendering at high zoom levels
2. **SDF Border Support**: Add Signed Distance Field borders for sub-pixel accuracy
3. **Brush Shapes**: Support for circular, rectangular, and custom brush shapes
4. **Edit Preview**: Show edit preview before committing changes
5. **Batch Operations**: Support for applying multiple edits atomically
6. **Compression**: Compress tile data for reduced storage requirements

## Performance Considerations

- The authoritative grid uses ~1GB for a full 16384×8192×4 byte grid
- Tiles are loaded on-demand to minimize memory usage
- LOD rebuilds are queued and processed incrementally
- Edit delta logs are size-limited to prevent unbounded growth

## Dependencies

- **SkiaSharp**: Graphics rendering and bitmap operations
- **System.Text.Json**: Edit delta serialization
- **System.Drawing**: Rectangle and Point structures
- **Avalonia**: UI framework integration