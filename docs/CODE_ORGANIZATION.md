# Code Organization

This document explains the file structure and organization of the Economy Sim codebase.

## Table of Contents
1. [Directory Structure](#directory-structure)
2. [Core Files](#core-files)
3. [System Categories](#system-categories)
4. [Views and UI](#views-and-ui)
5. [Helper Classes](#helper-classes)
6. [File Naming Conventions](#file-naming-conventions)

---

## Directory Structure

```
economy-sim/
│
├── docs/                           # Documentation (you are here!)
│   ├── README.md                   # Documentation index
│   ├── ARCHITECTURE.md             # System architecture
│   ├── FLOWCHARTS.md               # Visual diagrams
│   ├── GAME_SYSTEMS.md             # Game mechanics
│   ├── DATA_MODEL.md               # Data structures
│   ├── GETTING_STARTED.md          # Setup guide
│   ├── CODE_ORGANIZATION.md        # This file
│   └── UI_STRUCTURE.md             # UI documentation
│
├── Views/                          # Avalonia XAML views
│   ├── GameView.axaml              # Main game window (XAML)
│   ├── GameView.axaml.cs           # Game window code-behind
│   ├── MainWindow.axaml            # Main menu
│   ├── MainWindow.axaml.cs         # Main menu code
│   ├── ConstructionWindow.axaml    # Construction UI
│   ├── FactoryStatsWindow.axaml    # Factory statistics
│   ├── PolicyManagerWindow.axaml   # Policy management
│   ├── TradeProposalWindow.axaml   # Trade interface
│   ├── MapEditorWindow.axaml       # Map editing tools
│   └── ...                         # Other windows
│
├── Assets/                         # Images and resources
│   └── (images, icons, etc.)
│
├── Examples/                       # Example code
│   └── WorldGenerationExample.cs
│
├── data/                           # Generated data (not in git)
│   ├── tiles/                      # Map tiles
│   └── ...
│
├── logs/                           # Log files (not in git)
│
├── *.cs                            # C# source files (root)
├── *.py                            # Python utility scripts
├── *.md                            # Documentation files
├── Economy sim.csproj              # Project file
├── Economy sim.sln                 # Solution file
├── App.axaml                       # Application XAML
├── App.axaml.cs                    # Application entry point
├── Program.cs                      # Main entry point
└── world_setup.json                # World configuration (optional)
```

---

## Core Files

### Entry Points

#### `Program.cs`
- **Purpose**: Application entry point
- **Key Functions**:
  - `Main()` - Starts application
  - `GdalInit.Ensure()` - Initializes GDAL libraries
  - `BuildAvaloniaApp()` - Configures Avalonia

**Location**: Root directory

#### `App.axaml.cs`
- **Purpose**: Avalonia application class
- **Key Functions**:
  - `Initialize()` - Loads XAML
  - `OnFrameworkInitializationCompleted()` - Creates main window

**Location**: Root directory

---

## System Categories

Files are organized by functional domain. Here's how to find what you need:

### Economy System

| File | Purpose |
|------|---------|
| `Economy.cs` | Core economic simulation, factory production, initialization |
| `Market.cs` | Good definitions, market mechanics (if separate file) |
| `Good.cs` | Good class definition (may be in Economy.cs) |
| `Factory.cs` | Factory class definition (may be in Economy.cs) |
| `FactoryBlueprints.cs` | Factory templates (may be in Economy.cs) |

**Key Classes**: `Economy`, `Factory`, `FactoryBlueprint`, `Good`, `Market`

### Political System

| File | Purpose |
|------|---------|
| `Country.cs` | Country entity |
| `State.cs` | State entity |
| `City.cs` | City entity |
| `Government.cs` | Government, political parties, laws |
| `NationalFinancialSystem.cs` | National treasury, taxes, monetary policy |

**Key Classes**: `Country`, `State`, `City`, `Government`, `PoliticalParty`, `NationalFinancialSystem`

### Trade System

| File | Purpose |
|------|---------|
| `GlobalMarket.cs` | International marketplace |
| `InternationalTrade.cs` | Cross-border trade logic |
| `TradeRoutes.cs` | Established trade routes |
| `EnhancedTrade.cs` | Advanced trade features |
| `Diplomacy.cs` | Diplomatic relations affecting trade |

**Key Classes**: `GlobalMarket`, `TradeOffer`, `TradeRoute`, `TradeAgreement`

### Construction System

| File | Purpose |
|------|---------|
| `ConstructionCompany.cs` | Construction companies |
| `ConstructionProject.cs` | Ongoing construction projects |
| `ProceduralCityBuilder.cs` | Procedural city layout generation |
| `Building.cs` | Building entity |
| `BuildingRefiner.cs` | Building detail generation |

**Key Classes**: `ConstructionCompany`, `ConstructionProject`, `Building`

### Corporation System

| File | Purpose |
|------|---------|
| `Corporation.cs` | Corporation entity (may be in Economy.cs) |

**Key Classes**: `Corporation`, `CorporationSpecialization`

### Map & Rendering System

| File | Purpose |
|------|---------|
| `MultiResolutionMapManager.cs` | Multi-zoom map management |
| `HybridMapManager.cs` | Combined raster/vector rendering |
| `PoliticalBorderManager.cs` | Political boundary rendering |
| `GridRenderer.cs` | Tile-based rendering |
| `PopulationDensityRenderer.cs` | Population visualization |
| `PoliticalEntityRenderer.cs` | Political entity rendering |
| `GridControlEngine.cs` | Grid interaction |
| `MapViewLevel.cs` | Zoom level definitions |
| `MapViewType.cs` | Map display types |

**Key Classes**: `MultiResolutionMapManager`, `HybridMapManager`, `PoliticalBorderManager`

### Procedural Generation

| File | Purpose |
|------|---------|
| `ProceduralWorldGenerator.cs` | World generation from templates |
| `WorldDataGenerator.cs` | Generate world structure data |
| `ProceduralCityBuilder.cs` | Generate city layouts |
| `CityTemplates.cs` | City templates |
| `CityProceduralData.cs` | City generation data |

**Key Classes**: `ProceduralWorldGenerator`, `WorldDataGenerator`

### Data Structures

| File | Purpose |
|------|---------|
| `WorldDataStructures.cs` | DTOs for world data |
| `DataFileNames.cs` | File path constants |

**Key Classes**: `WorldSetupData`, `CountryData`, `StateData`, `CityData`

### Player System

| File | Purpose |
|------|---------|
| `PlayerRoleManager.cs` | Player role switching and control |

**Key Classes**: `PlayerRoleManager`

### Utilities

| File | Purpose |
|------|---------|
| `DebugLogger.cs` | Logging utility |
| `PerformanceTracker.cs` | Performance monitoring |
| `DialogHelper.cs` | UI dialog helpers |
| `CoordinateTransform.cs` | Coordinate conversion |
| `CoordinateValidation.cs` | Coordinate validation |
| `GeoBounds.cs` | Geographic bounding boxes |
| `GeoBoundsExtensions.cs` | Extension methods for GeoBounds |
| `GeometryUtil.cs` | Geometric calculations |
| `LineSegment.cs` | Line segment utility |
| `QuantizedPoint.cs` | Quantized coordinate points |
| `TileKey.cs` | Map tile identifier |

### Land Use & Economics

| File | Purpose |
|------|---------|
| `LandUseSimulator.cs` | Land use simulation |
| `LandValueCalculator.cs` | Property value calculation |
| `Parcel.cs` | Land parcel entity |
| `Suburb.cs` | Suburb entity |

### GIS & Map Data

| File | Purpose |
|------|---------|
| `CountryMaskGenerator.cs` | Generate country masks |
| `OptimizedPoliticalMaskGenerator.cs` | Optimized mask generation |
| `PixelMapGenerator.cs` | Generate pixel maps |
| `PoliticalDataCache.cs` | Cache political entity data |
| `PoliticalSpatialIndex.cs` | Spatial indexing for political entities |
| `PoliticalTileManager.cs` | Manage political boundary tiles |
| `StateBorderManager.cs` | State border management |

### Testing

| File | Purpose |
|------|---------|
| `GridSystemTest.cs` | Grid system tests |
| `GridIntegrationTest.cs` | Grid integration tests |
| `PoliticalBorderIntegrationTest.cs` | Border system tests |

---

## Views and UI

### Main Windows

| File | Purpose | Trigger |
|------|---------|---------|
| `MainWindow.axaml[.cs]` | Main menu | Application start |
| `GameView.axaml[.cs]` | Main game interface | New Game button |
| `OptionsWindow.axaml[.cs]` | Settings | Options button |

### Feature Windows

| File | Purpose | Access |
|------|---------|--------|
| `ConstructionWindow.axaml[.cs]` | Build factories | Player action |
| `FactoryStatsWindow.axaml[.cs]` | View factory details | Click factory |
| `PolicyManagerWindow.axaml[.cs]` | Manage policies | Government menu |
| `TradeProposalWindow.axaml[.cs]` | Trade interface | Trade menu |
| `DiplomaticRelationsWindow.axaml[.cs]` | Diplomacy | Diplomacy menu |
| `MapEditorWindow.axaml[.cs]` | Edit map | Developer tool |
| `PerformanceStatsWindow.axaml[.cs]` | Performance metrics | Debug menu |
| `PopStatsWindow.axaml[.cs]` | Population statistics | Stats menu |
| `LoadingWindow.axaml[.cs]` | Loading screen | During world load |

### XAML Files

Each window has two files:
- `.axaml` - UI definition (XAML markup)
- `.axaml.cs` - Code-behind (C# event handlers, logic)

---

## Helper Classes

### Coordinate & Geography

- `CoordinateTransform` - Convert between coordinate systems
- `CoordinateValidation` - Validate coordinates
- `GeoBounds` - Geographic bounding boxes
- `GeometryUtil` - Geometric calculations
- `LineSegment` - Line segment operations

### Performance & Debugging

- `PerformanceTracker` - Monitor performance
- `DebugLogger` - Logging utility

### UI Helpers

- `DialogHelper` - Simplify dialog creation

---

## File Naming Conventions

### C# Files

**Pattern**: `PascalCase.cs`

**Examples**:
- `Country.cs` - Single class file
- `MultiResolutionMapManager.cs` - Multi-word name
- `WorldDataStructures.cs` - Multiple related classes

### XAML Files

**Pattern**: `WindowName.axaml` + `WindowName.axaml.cs`

**Examples**:
- `GameView.axaml` + `GameView.axaml.cs`
- `FactoryStatsWindow.axaml` + `FactoryStatsWindow.axaml.cs`

### Python Scripts

**Pattern**: `snake_case.py`

**Examples**:
- `generate_world.py`
- `fetch_etopo1.py`
- `create_country_mask.py`

### Documentation

**Pattern**: `UPPER_SNAKE_CASE.md` or `Title_Case.md`

**Examples**:
- `README.md`
- `ARCHITECTURE.md`
- `Procedural Generation and Dynamic Economy.md`

---

## Finding Specific Code

### "How do I find where X is implemented?"

| What You're Looking For | Where to Look |
|-------------------------|---------------|
| Factory production logic | `Economy.cs` |
| Country/state/city entities | `Country.cs`, `State.cs`, `City.cs` |
| Market pricing | `Economy.cs` or `GlobalMarket.cs` |
| Trade execution | `GlobalMarket.cs`, `InternationalTrade.cs` |
| Map rendering | `MultiResolutionMapManager.cs`, `HybridMapManager.cs` |
| Construction process | `ConstructionCompany.cs`, `ConstructionProject.cs` |
| World generation | `ProceduralWorldGenerator.cs`, `WorldDataGenerator.cs` |
| UI windows | `Views/` directory |
| Player roles | `PlayerRoleManager.cs` |
| Government/policies | `Government.cs`, `NationalFinancialSystem.cs` |

### Search Tips

**Find class definition**:
```bash
grep -r "class YourClassName" *.cs
```

**Find method usage**:
```bash
grep -r "MethodName" *.cs
```

**Find file by name**:
```bash
find . -name "*YourFileName*"
```

---

## Code Layout Best Practices

When adding new code:

1. **Single Responsibility**: One class per file (when practical)
2. **Related Classes**: Group tightly coupled classes in same file
3. **Namespace**: Use `Economy_sim` namespace
4. **Regions**: Use `#region` for large files (sparingly)
5. **Comments**: Add XML documentation for public APIs

Example:
```csharp
namespace Economy_sim
{
    /// <summary>
    /// Represents a new game entity
    /// </summary>
    public class NewEntity
    {
        // Implementation
    }
}
```

---

**Related Documentation**:
- [Architecture Overview](ARCHITECTURE.md) - How systems connect
- [Getting Started](GETTING_STARTED.md) - Setup and build
- [Game Systems Guide](GAME_SYSTEMS.md) - System details
- [Data Model](DATA_MODEL.md) - Entity relationships
