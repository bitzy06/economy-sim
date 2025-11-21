# Architecture Overview

## Table of Contents
1. [High-Level Architecture](#high-level-architecture)
2. [Component Diagram](#component-diagram)
3. [Core Systems](#core-systems)
4. [Data Flow](#data-flow)
5. [Technology Stack](#technology-stack)
6. [Design Patterns](#design-patterns)

## High-Level Architecture

Economy Sim uses a **layered architecture** with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                     Presentation Layer                       │
│  (Avalonia UI - XAML Views, Code-Behind, ViewModels)        │
└─────────────────────────────────────────────────────────────┘
                              ↕
┌─────────────────────────────────────────────────────────────┐
│                      Game Logic Layer                        │
│  (Economy Systems, Trade, Government, Construction)          │
└─────────────────────────────────────────────────────────────┘
                              ↕
┌─────────────────────────────────────────────────────────────┐
│                        Data Layer                            │
│  (Entities: Country, State, City, Factory, Corporation)      │
└─────────────────────────────────────────────────────────────┘
                              ↕
┌─────────────────────────────────────────────────────────────┐
│                   Infrastructure Layer                       │
│  (GIS Processing, Map Rendering, File I/O, Utilities)       │
└─────────────────────────────────────────────────────────────┘
```

## Component Diagram

### Core Components and Relationships

```
                    ┌──────────────┐
                    │   MainWindow │
                    └──────┬───────┘
                           │
                           ↓
                    ┌──────────────┐
                    │   GameView   │◄──────────┐
                    └──────┬───────┘           │
                           │                   │
                           │                   │
        ┌──────────────────┼──────────────────┼────────────────┐
        │                  │                  │                │
        ↓                  ↓                  ↓                ↓
┌───────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ Economy System│  │ Map Rendering│  │  UI Windows  │  │Player Manager│
└───────┬───────┘  └──────┬───────┘  └──────────────┘  └──────────────┘
        │                 │
        │                 │
        ↓                 ↓
┌───────────────────────────────────────────────────────────┐
│                   World Entities                          │
│                                                           │
│  ┌─────────┐    ┌─────────┐    ┌──────────┐            │
│  │ Country │───→│  State  │───→│   City   │            │
│  └─────────┘    └─────────┘    └────┬─────┘            │
│                                      │                   │
│                   ┌──────────────────┼──────────┐       │
│                   ↓                  ↓          ↓       │
│            ┌──────────┐      ┌──────────┐  ┌─────────┐ │
│            │ Factory  │      │Population│  │Building │ │
│            └────┬─────┘      └──────────┘  └─────────┘ │
│                 │                                        │
│                 ↓                                        │
│          ┌─────────────┐                                │
│          │Corporation  │                                │
│          └─────────────┘                                │
└───────────────────────────────────────────────────────────┘
                           │
                           ↓
               ┌────────────────────────┐
               │   Market & Trade       │
               │  - GlobalMarket        │
               │  - InternationalTrade  │
               │  - TradeRoutes         │
               └────────────────────────┘
```

## Core Systems

### 1. Economic System

**Purpose**: Manages production, consumption, and trade of goods

**Key Components**:
- `Economy.cs` - Core economic simulation logic
- `Factory` - Production facilities with inputs/outputs
- `FactoryBlueprint` - Templates for factory types
- `Good` - Tradeable commodities
- `Market` - Price discovery and trade execution
- `Corporation` - Economic actors owning factories

**Flow**:
```
Factory Production Cycle:
┌─────────────────────────────────────────────────────────┐
│  1. Check Input Availability                            │
│  2. Consume Input Goods from City Stockpile             │
│  3. Produce Output Goods (based on capacity & workers)  │
│  4. Add Output to City Stockpile                        │
│  5. Update Market Prices based on Supply/Demand         │
└─────────────────────────────────────────────────────────┘
```

### 2. Political System

**Purpose**: Manages countries, states, and governmental functions

**Key Components**:
- `Country` - Top-level political entity
- `State` - Regional subdivision
- `Government` - Political parties and policies
- `NationalFinancialSystem` - Treasury, taxes, budgets

**Hierarchy**:
```
Country
  ├── Government (Policies, Parties)
  ├── Financial System (Budget, Taxes)
  └── States[]
        ├── Cities[]
        │     ├── Factories[]
        │     ├── Population
        │     └── Buildings[]
        └── Local Budget
```

### 3. Map & Rendering System

**Purpose**: Visualize the world with geographical data

**Key Components**:
- `MultiResolutionMapManager` - Multi-zoom level map management
- `HybridMapManager` - Combined raster/vector rendering
- `PoliticalBorderManager` - Country/state borders
- `GridRenderer` - Tile-based rendering
- `PopulationDensityRenderer` - Population visualization

**Rendering Pipeline**:
```
1. Load Base Map Data (GeoTIFF, Shapefiles)
2. Generate Tiles at Multiple Zoom Levels
3. Apply Political Borders
4. Overlay Population Density
5. Render to SkiaSharp Canvas
6. Display in Avalonia UI
```

### 4. Construction System

**Purpose**: Build new factories and infrastructure

**Key Components**:
- `ConstructionCompany` - Build factories for payment
- `ConstructionProject` - Tracks ongoing construction
- `ProceduralCityBuilder` - Generate city layouts

**Construction Flow**:
```
Request Construction
       ↓
Check Budget & Materials
       ↓
Create Project (Duration-based)
       ↓
Progress Over Time
       ↓
Complete & Add Factory to City
```

### 5. Trade System

**Purpose**: Enable local and international trade

**Key Components**:
- `GlobalMarket` - International trade clearing house
- `InternationalTrade` - Cross-border trade logic
- `TradeRoutes` - Established trade connections
- `EnhancedTrade` - Advanced trade features

**Trade Flow**:
```
Seller Lists Goods → Global Market → Buyer Purchases
                          ↓
                   Price Discovery
                          ↓
                   Transaction Logs
```

## Data Flow

### Game Loop Data Flow

```
┌─────────────────────────────────────────────────────┐
│              Main Game Loop (GameView)              │
└──────────────────┬──────────────────────────────────┘
                   │
                   ↓
        ┌──────────────────────┐
        │  Turn Advancement    │
        └──────────┬───────────┘
                   │
        ┌──────────┼───────────────────┐
        ↓          ↓                   ↓
  ┌─────────┐  ┌─────────┐     ┌──────────────┐
  │Production│  │Trade    │     │Government    │
  │  Cycle   │  │ Cycle   │     │   Actions    │
  └────┬─────┘  └────┬────┘     └──────┬───────┘
       │             │                  │
       └─────────────┴──────────────────┘
                     │
                     ↓
          ┌──────────────────┐
          │  Update UI       │
          │  - Statistics    │
          │  - Map Display   │
          │  - Event Logs    │
          └──────────────────┘
```

### Economy Update Cycle

```
For Each City:
  1. Calculate Population Consumption Needs
  2. For Each Factory:
       a. Check Input Availability
       b. Produce Goods (if inputs available)
       c. Pay Workers
       d. Add Output to City Stockpile
  3. Update Local Market Prices
  4. Execute Local Trade
  5. Export/Import via International Trade
  6. Collect Taxes
  7. Pay City Expenses
```

## Technology Stack

### Core Technologies
- **.NET 8.0** - Runtime and base framework
- **C# 12** - Primary programming language
- **Avalonia 11.3.2** - Cross-platform UI framework

### Key Libraries

| Library | Purpose |
|---------|---------|
| **MaxRev.Gdal.Core** | Geospatial data processing |
| **NetTopologySuite** | Geometric operations |
| **SkiaSharp** | 2D graphics rendering |
| **ImageSharp** | Image processing |
| **CommunityToolkit.Mvvm** | MVVM pattern helpers |
| **MessageBox.Avalonia** | Dialog boxes |

### Python Utilities
- **Python 3.10+** - Data generation scripts
- **fiona** - Vector file I/O
- **rasterio** - Raster data processing
- **shapely** - Geometric operations

## Design Patterns

### 1. **Singleton Pattern**
- `GlobalMarket.Instance` - Single market instance
- `PerformanceTracker` - Single performance monitor

### 2. **Factory Pattern**
- `FactoryBlueprint` - Template for creating factories
- `ProceduralWorldGenerator` - Generate world entities

### 3. **Observer Pattern**
- `Economy.ConstructionProgressed` - Event notifications
- Various UI update events

### 4. **Repository Pattern**
- `PoliticalDataCache` - Cache political entity data
- `WorldDataStructures` - Data transfer objects

### 5. **Strategy Pattern**
- `CorporationSpecialization` - Different business strategies
- `PlayerRoleManager` - Different player roles (PM, Governor, CEO)

### 6. **MVVM Pattern**
- Views (XAML) - UI definition
- ViewModels - UI logic and state
- Models - Business entities

## Key Architectural Decisions

### Multi-Resolution Map System
- **Why**: Handle large geographical datasets efficiently
- **How**: Pre-generate tiles at multiple zoom levels
- **Trade-off**: Storage space for rendering speed

### Procedural World Generation
- **Why**: Create diverse, realistic economies
- **How**: Templates + randomization
- **Benefit**: Replayability and scalability

### Event-Driven Updates
- **Why**: Decouple UI from business logic
- **How**: Events for state changes
- **Benefit**: Maintainability and testability

### Thread-Safe Collections
- **Why**: Avoid concurrent modification issues
- **How**: Lock-based synchronization in City, Factory
- **Trade-off**: Slight performance overhead for safety

## System Interactions

### How Components Work Together

```
Player Action (UI Click)
        ↓
GameView Event Handler
        ↓
Call Business Logic (e.g., ConstructionCompany.BuildFactory)
        ↓
Update Data Model (Add Factory to City)
        ↓
Fire Event (Economy.ConstructionProgressed)
        ↓
UI Listens & Updates Display
        ↓
Render Changes to Screen
```

---

**Related Documentation**:
- [Game Systems Guide](GAME_SYSTEMS.md) - Detailed system mechanics
- [Data Model](DATA_MODEL.md) - Entity relationships
- [Flow Charts](FLOWCHARTS.md) - Visual process diagrams
- [Code Organization](CODE_ORGANIZATION.md) - File structure
