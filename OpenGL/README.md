# OpenGL Implementation Guide

This document describes the OpenTK/OpenGL integration implemented in the Economy Simulation project.

## Overview

The OpenGL implementation provides hardware-accelerated map rendering as an alternative to the existing SkiaSharp CPU-based rendering. The implementation uses OpenTK (a .NET wrapper for OpenGL) to provide modern GPU acceleration while maintaining compatibility with the existing codebase.

## Architecture

### Key Components

1. **OpenGLControl.cs** - Avalonia-compatible OpenGL control
2. **OpenGLMapRenderer.cs** - Core OpenGL rendering logic with shaders
3. **Toggle Integration** - UI controls to switch between rendering modes

### Design Principles

- **Minimal Changes**: The implementation preserves the existing UI structure and map management systems
- **Hybrid Approach**: Both SkiaSharp and OpenGL renderers coexist, allowing runtime switching
- **Modern OpenGL**: Uses OpenGL 3.3+ with vertex/fragment shaders for optimal performance
- **Input Compatibility**: Mouse pan/zoom functionality works identically in both rendering modes

## Usage

### Switching Rendering Modes

1. Launch the application
2. Click the "OpenGL" button in the right panel to switch to OpenGL rendering
3. Click "SkiaSharp" to switch back to CPU rendering

### Features Available in OpenGL Mode

- **Hardware Acceleration**: Map rendering utilizes GPU resources
- **Smooth Pan/Zoom**: Mouse wheel zooming and drag panning
- **Texture Mapping**: Efficient texture-based map rendering
- **Real-time Updates**: Dynamic map switching (terrain/political views)

## Technical Details

### OpenGL Setup

The implementation uses:
- OpenGL 3.3 Core Profile
- Vertex Array Objects (VAO) for geometry
- Vertex Buffer Objects (VBO) for vertex data
- Element Buffer Objects (EBO) for indices
- Texture2D for map image data

### Shaders

**Vertex Shader:**
- Transforms vertex positions using projection, view, and model matrices
- Passes texture coordinates to fragment shader

**Fragment Shader:**
- Samples texture using interpolated coordinates
- Outputs final pixel color

### Integration Points

1. **GameView.axaml**: Added toggle button for renderer switching
2. **GameView.axaml.cs**: Integrated OpenGL control management
3. **Map Data Flow**: OpenGL renderer receives SKBitmap data from HybridMapManager
4. **Input Handling**: OpenGL control handles mouse events for pan/zoom

## Development Notes

### Building

The project requires:
- .NET 8.0 SDK
- OpenTK 4.9.4 (automatically installed via NuGet)
- Existing Avalonia and SkiaSharp dependencies

### Testing

Run the component tests:
```csharp
Economy_sim.Tests.OpenGLComponentTest.RunBasicTests();
```

### Performance Considerations

- OpenGL rendering offloads work to GPU, reducing CPU usage
- Texture updates are optimized for real-time map switching
- Memory management handles both CPU and GPU resources

### Future Enhancements

Potential improvements:
1. Implement multiple texture layers for complex map overlays
2. Add shader-based effects (highlighting, filtering)
3. Optimize texture streaming for large maps
4. Add vertex buffer-based rendering for vector data

## Troubleshooting

### Common Issues

1. **OpenGL Context Creation Failed**: Ensure graphics drivers support OpenGL 3.3+
2. **Shader Compilation Errors**: Check graphics driver compatibility
3. **Texture Upload Issues**: Verify texture size limits and format support

### Debugging

Enable debug output in OpenGLMapRenderer.cs by monitoring console output for:
- Shader compilation status
- Texture upload confirmation
- Rendering error messages

## Compatibility

- **Operating Systems**: Windows, Linux, macOS (where OpenGL 3.3+ is supported)
- **Graphics Cards**: Any GPU with OpenGL 3.3+ support
- **Fallback**: SkiaSharp renderer remains available as fallback option