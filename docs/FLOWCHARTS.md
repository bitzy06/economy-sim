# Flow Charts

This document contains visual flow charts for understanding key processes in Economy Sim.

## Table of Contents
1. [Game Initialization Flow](#game-initialization-flow)
2. [Production Cycle](#production-cycle)
3. [Market & Trade Flow](#market--trade-flow)
4. [Construction Process](#construction-process)
5. [Turn Processing](#turn-processing)
6. [Player Action Flow](#player-action-flow)
7. [Map Rendering Pipeline](#map-rendering-pipeline)
8. [Data Loading & World Generation](#data-loading--world-generation)

---

## Game Initialization Flow

### Application Startup

```
┌─────────────────────────────────────────────────────────────┐
│                    Program.Main()                            │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ↓
                ┌────────────────────┐
                │  Initialize GDAL   │
                │  (GIS Libraries)   │
                └─────────┬──────────┘
                          │
                          ↓
                ┌─────────────────────┐
                │   Build Avalonia    │
                │   Application       │
                └──────────┬──────────┘
                           │
                           ↓
                 ┌─────────────────────┐
                 │  App.Initialize()   │
                 │  Load XAML          │
                 └──────────┬──────────┘
                            │
                            ↓
                  ┌──────────────────────┐
                  │  Show MainWindow     │
                  │  (Main Menu)         │
                  └──────────────────────┘
```

### New Game Creation

```
┌───────────────────────────────────────────────────────────────┐
│            User Clicks "New Game" Button                      │
└────────────────────────┬──────────────────────────────────────┘
                         │
                         ↓
              ┌──────────────────────┐
              │ Create GameView      │
              │ Window Instance      │
              └──────────┬───────────┘
                         │
                         ↓
         ┌───────────────────────────────────┐
         │  GameView Constructor             │
         └───────────┬───────────────────────┘
                     │
          ┌──────────┼───────────────┐
          ↓          ↓               ↓
    ┌─────────┐ ┌────────┐    ┌──────────────┐
    │Init UI  │ │Init Map│    │Init Variables│
    └─────────┘ └────────┘    └──────────────┘
                     │
                     ↓
         ┌────────────────────────┐
         │ LoadWorldDataAsync()   │
         └───────────┬────────────┘
                     │
          ┌──────────┼──────────────────┐
          ↓          ↓                  ↓
    ┌──────────┐ ┌──────────┐  ┌────────────────┐
    │ Generate │ │ Load Map │  │ Initialize     │
    │ World    │ │ Data     │  │ Economy System │
    └────┬─────┘ └────┬─────┘  └────┬───────────┘
         │            │              │
         └────────────┴──────────────┘
                      │
                      ↓
           ┌──────────────────────┐
           │ Economy.Initialize   │
           │ WorldEconomy()       │
           └──────────┬───────────┘
                      │
           ┌──────────┼──────────────────┐
           ↓          ↓                  ↓
    ┌────────────┐ ┌─────────┐  ┌────────────────┐
    │ Create     │ │ Create  │  │ Setup          │
    │ Countries  │ │ Factories│  │ Corporations   │
    └────────────┘ └─────────┘  └────────────────┘
                      │
                      ↓
              ┌───────────────┐
              │ Start Game    │
              │ Loop Timer    │
              └───────────────┘
```

---

## Production Cycle

### Factory Production Step-by-Step

```
                    ┌────────────────────┐
                    │  Production Turn   │
                    │  Begins            │
                    └─────────┬──────────┘
                              │
                              ↓
                    ┌─────────────────────┐
                    │ For Each City       │
                    └──────────┬──────────┘
                               │
                               ↓
                    ┌──────────────────────┐
                    │ For Each Factory     │
                    └──────────┬───────────┘
                               │
                               ↓
              ┌────────────────────────────────┐
              │ Check if Factory has Inputs    │
              │ Required for Production        │
              └────────┬───────────────────────┘
                       │
            ┌──────────┴─────────┐
            ↓                    ↓
      ┌──────────┐         ┌──────────┐
      │   YES    │         │    NO    │
      └─────┬────┘         └────┬─────┘
            │                   │
            ↓                   ↓
  ┌───────────────────┐   ┌──────────────┐
  │ Calculate Workers │   │ Skip Factory │
  │ & Efficiency      │   │ (No Output)  │
  └────────┬──────────┘   └──────────────┘
           │
           ↓
  ┌────────────────────┐
  │ Consume Input      │
  │ Goods from City    │
  │ Stockpile          │
  └────────┬───────────┘
           │
           ↓
  ┌────────────────────────┐
  │ Produce Output Goods   │
  │ Amount = Capacity ×    │
  │ Worker Efficiency      │
  └────────┬───────────────┘
           │
           ↓
  ┌────────────────────┐
  │ Add Output to      │
  │ City Stockpile     │
  └────────┬───────────┘
           │
           ↓
  ┌────────────────────┐
  │ Pay Workers        │
  │ (Wages)            │
  └────────┬───────────┘
           │
           ↓
  ┌────────────────────┐
  │ Corporation        │
  │ Receives Profit    │
  └────────────────────┘
```

### Production with Market Interaction

```
┌──────────────────────────────────────────────────────────────┐
│                    Start Production Cycle                     │
└────────────────────────┬─────────────────────────────────────┘
                         │
         ┌───────────────┴───────────────┐
         ↓                               ↓
┌─────────────────┐            ┌──────────────────┐
│ Local Production│            │ Check Stockpile  │
│ (See above)     │            │ vs. Demand       │
└────────┬────────┘            └────────┬─────────┘
         │                              │
         │                    ┌─────────┴─────────┐
         │                    ↓                   ↓
         │            ┌──────────────┐    ┌─────────────┐
         │            │ Surplus?     │    │ Shortage?   │
         │            └──────┬───────┘    └──────┬──────┘
         │                   │                   │
         │                   ↓                   ↓
         │         ┌──────────────────┐  ┌──────────────┐
         │         │ Lower Prices     │  │ Raise Prices │
         │         │ Export to Global │  │ Import from  │
         │         │ Market           │  │ Global Market│
         │         └──────────────────┘  └──────────────┘
         │
         └──────────────────┬────────────────────────────┘
                            │
                            ↓
                  ┌──────────────────┐
                  │ Update Market    │
                  │ Statistics       │
                  └──────┬───────────┘
                         │
                         ↓
                  ┌──────────────────┐
                  │ Trigger UI Update│
                  └──────────────────┘
```

---

## Market & Trade Flow

### Local Market Price Discovery

```
┌───────────────────────────────────────────────────────────┐
│                  Market Price Update                       │
└────────────────────────┬──────────────────────────────────┘
                         │
                         ↓
              ┌──────────────────────┐
              │ For Each Good Type   │
              └──────────┬───────────┘
                         │
                         ↓
         ┌───────────────────────────────┐
         │ Calculate Supply              │
         │ (Production + Stockpile)      │
         └───────────┬───────────────────┘
                     │
                     ↓
         ┌───────────────────────────────┐
         │ Calculate Demand              │
         │ (Population Needs + Factory   │
         │  Input Requirements)          │
         └───────────┬───────────────────┘
                     │
                     ↓
         ┌───────────────────────────────┐
         │ Supply vs Demand Ratio        │
         └───────────┬───────────────────┘
                     │
          ┌──────────┼──────────┐
          ↓          ↓          ↓
    ┌─────────┐ ┌────────┐ ┌─────────┐
    │Surplus  │ │Balanced│ │Shortage │
    │Supply>  │ │Supply≈ │ │Supply<  │
    │Demand   │ │Demand  │ │Demand   │
    └────┬────┘ └───┬────┘ └────┬────┘
         │          │           │
         ↓          ↓           ↓
    ┌─────────┐ ┌────────┐ ┌─────────┐
    │Decrease │ │Keep    │ │Increase │
    │Price    │ │Price   │ │Price    │
    │(-5%)    │ │Stable  │ │(+5%)    │
    └─────────┘ └────────┘ └─────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Update Price in Market    │
         └───────────────────────────┘
```

### International Trade Process

```
┌──────────────────────────────────────────────────────────┐
│         Corporation Wants to Sell Goods                  │
└────────────────────┬─────────────────────────────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ List Goods on Global      │
         │ Market                    │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Set Price (Based on       │
         │ Production Cost + Margin) │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Goods Available           │
         │ Internationally           │
         └───────────────────────────┘


┌──────────────────────────────────────────────────────────┐
│      Corporation/City Needs to Buy Goods                 │
└────────────────────┬─────────────────────────────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Check Local Market        │
         └───────────┬───────────────┘
                     │
          ┌──────────┴─────────┐
          ↓                    ↓
    ┌──────────┐         ┌──────────┐
    │Available │         │Not Avail.│
    │Locally   │         │          │
    └────┬─────┘         └────┬─────┘
         │                    │
         ↓                    ↓
    ┌──────────┐    ┌────────────────────┐
    │Purchase  │    │Search Global Market│
    │Locally   │    └────────┬───────────┘
    └──────────┘             │
                  ┌──────────┴─────────┐
                  ↓                    ↓
            ┌──────────┐         ┌──────────┐
            │Found on  │         │Not Found │
            │Global    │         │          │
            │Market    │         └────┬─────┘
            └────┬─────┘              │
                 │                    ↓
                 ↓              ┌──────────┐
         ┌──────────────┐      │Production│
         │ Check Budget │      │Delayed   │
         └──────┬───────┘      └──────────┘
                │
      ┌─────────┴─────────┐
      ↓                   ↓
┌──────────┐        ┌──────────┐
│Sufficient│        │Insufficient│
│Funds     │        │Funds      │
└────┬─────┘        └─────┬─────┘
     │                    │
     ↓                    ↓
┌──────────┐        ┌──────────┐
│Purchase  │        │Cannot Buy│
│& Import  │        │(Shortage)│
└──────────┘        └──────────┘
```

---

## Construction Process

### Building a New Factory

```
┌──────────────────────────────────────────────────────────┐
│     Player/Corporation Requests Factory Construction     │
└────────────────────┬─────────────────────────────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Select Factory Type       │
         │ (FactoryBlueprint)        │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Select Location (City)    │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Find Construction Company │
         └───────────┬───────────────┘
                     │
          ┌──────────┴─────────┐
          ↓                    ↓
    ┌──────────┐         ┌──────────┐
    │Available │         │No Company│
    │Company   │         │Available │
    └────┬─────┘         └────┬─────┘
         │                    │
         │                    ↓
         │              ┌──────────┐
         │              │Cannot    │
         │              │Build     │
         │              └──────────┘
         ↓
┌────────────────────────────┐
│ Calculate Construction Cost│
│ (Base Cost × Size)         │
└────────────┬───────────────┘
             │
             ↓
┌────────────────────────────┐
│ Check Requester Budget     │
└────────────┬───────────────┘
             │
  ┌──────────┴─────────┐
  ↓                    ↓
┌──────────┐     ┌──────────────┐
│Sufficient│     │Insufficient  │
│Budget    │     │Budget        │
└────┬─────┘     └──────┬───────┘
     │                  │
     │                  ↓
     │            ┌──────────┐
     │            │Construction│
     │            │Cancelled  │
     │            └──────────┘
     ↓
┌────────────────────────────┐
│ Deduct Cost from Budget    │
└────────────┬───────────────┘
             │
             ↓
┌────────────────────────────┐
│ Create ConstructionProject │
│ - Duration: N turns        │
│ - Progress: 0%             │
└────────────┬───────────────┘
             │
             ↓
┌────────────────────────────┐
│ Add to City's Construction │
│ Queue                      │
└────────────┬───────────────┘
             │
             ↓
     ┌───────────────┐
     │ Each Turn:    │
     │ Progress += X%│
     └───────┬───────┘
             │
             ↓
┌────────────────────────────┐
│ When Progress = 100%       │
└────────────┬───────────────┘
             │
             ↓
┌────────────────────────────┐
│ Create Factory Instance    │
│ - Assign to Corporation    │
│ - Add to City              │
└────────────┬───────────────┘
             │
             ↓
┌────────────────────────────┐
│ Fire ConstructionProgressed│
│ Event                      │
└────────────┬───────────────┘
             │
             ↓
┌────────────────────────────┐
│ UI Updates to Show New     │
│ Factory                    │
└────────────────────────────┘
```

---

## Turn Processing

### Main Game Loop Turn

```
┌──────────────────────────────────────────────────────────┐
│              Timer Tick (Turn Advance)                   │
└────────────────────┬─────────────────────────────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Increment Turn Counter    │
         └───────────┬───────────────┘
                     │
         ┌───────────┴───────────────────────┐
         │                                   │
         ↓                                   ↓
┌─────────────────┐                ┌─────────────────┐
│ Economic Update │                │ Political Update│
└────────┬────────┘                └────────┬────────┘
         │                                  │
         │                                  │
┌────────┴────────────────────────┐         │
│                                 │         │
↓                                 ↓         ↓
┌──────────────┐         ┌────────────┐  ┌─────────┐
│ Production   │         │ Trade      │  │ Taxes   │
│ Cycle        │         │ Execution  │  │ Collection│
└──────┬───────┘         └──────┬─────┘  └────┬────┘
       │                        │             │
       └────────────────┬───────┴─────────────┘
                        │
                        ↓
              ┌──────────────────┐
              │ Population Update│
              │ - Consumption    │
              │ - Employment     │
              └────────┬─────────┘
                       │
                       ↓
              ┌──────────────────┐
              │ Construction     │
              │ Progress Update  │
              └────────┬─────────┘
                       │
                       ↓
              ┌──────────────────┐
              │ Calculate Stats  │
              │ - GDP            │
              │ - Unemployment   │
              │ - Budget Balance │
              └────────┬─────────┘
                       │
                       ↓
              ┌──────────────────┐
              │ Update UI        │
              │ - Stats Panel    │
              │ - Event Log      │
              │ - Map (if needed)│
              └──────────────────┘
```

### Detailed Turn Processing Phases

```
Phase 1: PRODUCTION
├── For each Country
│   ├── For each State
│   │   ├── For each City
│   │   │   ├── For each Factory
│   │   │   │   ├── Attempt Production
│   │   │   │   └── Update Stockpile
│   │   │   └── Calculate Local Supply/Demand
│   │   └── Update State Aggregates
│   └── Update Country Aggregates

Phase 2: TRADE
├── Update Local Prices (Supply/Demand)
├── Execute Local Trades
├── International Trade Matching
└── Transfer Goods & Payments

Phase 3: GOVERNMENT
├── Collect Taxes
│   ├── City → State
│   ├── State → Country
│   └── Income Tax from Workers
├── Government Spending
│   ├── City Expenses
│   ├── State Expenses
│   └── National Expenses
└── Update Budgets

Phase 4: CONSTRUCTION
├── Progress all Construction Projects
├── Complete Finished Projects
└── Add New Factories to Cities

Phase 5: STATISTICS
├── Calculate GDP
├── Calculate Unemployment Rate
├── Update Population Stats
└── Log Historical Data

Phase 6: UI UPDATE
├── Refresh Statistics Panels
├── Update Event Log
├── Redraw Map (if entities changed)
└── Update Player Role Info
```

---

## Player Action Flow

### Player Interaction with Game Systems

```
┌──────────────────────────────────────────────────────────┐
│                  Player UI Action                        │
└────────────────────┬─────────────────────────────────────┘
                     │
        ┌────────────┼────────────┐
        ↓            ↓            ↓
┌───────────┐  ┌──────────┐  ┌──────────┐
│Build      │  │Change    │  │View      │
│Factory    │  │Policy    │  │Statistics│
└─────┬─────┘  └────┬─────┘  └────┬─────┘
      │             │             │
      │             │             │
      ↓             ↓             ↓
┌──────────────────────────────────────────┐
│           GameView Event Handler          │
└──────────┬───────────────────────────────┘
           │
           ↓
┌──────────────────────────────┐
│  Validate Player Action      │
│  - Check Role Permissions    │
│  - Check Resources Available │
└──────────┬───────────────────┘
           │
   ┌───────┴────────┐
   ↓                ↓
┌──────┐      ┌──────────┐
│Valid │      │ Invalid  │
└───┬──┘      └────┬─────┘
    │              │
    │              ↓
    │         ┌──────────┐
    │         │Show Error│
    │         │Message   │
    │         └──────────┘
    ↓
┌──────────────────────────┐
│ Execute Business Logic   │
└──────────┬───────────────┘
           │
           ↓
┌──────────────────────────┐
│ Update Data Model        │
└──────────┬───────────────┘
           │
           ↓
┌──────────────────────────┐
│ Fire Events              │
└──────────┬───────────────┘
           │
           ↓
┌──────────────────────────┐
│ UI Updates Automatically │
│ via Event Handlers       │
└──────────────────────────┘
```

### Player Role System

```
┌──────────────────────────────────────────────────────────┐
│              Player Assumes Role                         │
└────────────────────┬─────────────────────────────────────┘
                     │
        ┌────────────┼────────────┐
        ↓            ↓            ↓
┌───────────┐  ┌──────────┐  ┌──────────┐
│Prime      │  │Governor  │  │CEO       │
│Minister   │  │          │  │          │
└─────┬─────┘  └────┬─────┘  └────┬─────┘
      │             │             │
      ↓             ↓             ↓
┌──────────┐  ┌──────────┐  ┌──────────┐
│Control   │  │Control   │  │Control   │
│Country   │  │State     │  │Corporation│
└─────┬────┘  └────┬─────┘  └────┬─────┘
      │            │             │
      └────────────┴─────────────┘
                   │
                   ↓
         ┌──────────────────┐
         │ Set Controlled   │
         │ Entity           │
         └────────┬─────────┘
                  │
                  ↓
         ┌──────────────────┐
         │ Update UI to Show│
         │ Role-Specific    │
         │ Controls         │
         └──────────────────┘

Actions by Role:
┌─────────────────────────────────────────────────────────┐
│ Prime Minister:                                         │
│  - Set National Policies                               │
│  - Manage National Budget                              │
│  - International Trade Agreements                      │
│  - View All Country Statistics                         │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ Governor:                                               │
│  - Set State Policies                                  │
│  - Manage State Budget                                 │
│  - Subsidize Local Industries                          │
│  - View State Statistics                               │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│ CEO (Corporation):                                      │
│  - Build New Factories                                 │
│  - Set Production Quotas                               │
│  - Manage Corporate Budget                             │
│  - Trade Goods                                         │
└─────────────────────────────────────────────────────────┘
```

---

## Map Rendering Pipeline

### Multi-Resolution Map Display

```
┌──────────────────────────────────────────────────────────┐
│          User Pans/Zooms Map                             │
└────────────────────┬─────────────────────────────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Calculate Viewport Bounds │
         │ (Lat/Lon Coordinates)     │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Determine Zoom Level      │
         │ (Based on Scale)          │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ MultiResolutionMapManager │
         │ .AssembleView()           │
         └───────────┬───────────────┘
                     │
         ┌───────────┴────────────────┐
         ↓                            ↓
┌─────────────────┐         ┌─────────────────┐
│ Load Base       │         │ Load Political  │
│ Terrain Tiles   │         │ Border Data     │
└────────┬────────┘         └────────┬────────┘
         │                           │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Composite Layers:         │
         │ 1. Terrain (base)         │
         │ 2. Political Borders      │
         │ 3. Population Density     │
         │ 4. Selected Entity        │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Render to SkiaSharp       │
         │ Canvas (SKBitmap)         │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Convert to Avalonia Image │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Display in UI             │
         └───────────────────────────┘
```

### Tile Generation (Preprocessing)

```
┌──────────────────────────────────────────────────────────┐
│        Load Source Geographical Data                     │
│        (GeoTIFF, Shapefiles, etc.)                       │
└────────────────────┬─────────────────────────────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ For Each Zoom Level       │
         │ (e.g., 0, 1, 2, 3)        │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Divide into Tiles         │
         │ (e.g., 256x256 pixels)    │
         └───────────┬───────────────┘
                     │
         ┌───────────┴────────────────┐
         ↓                            ↓
┌─────────────────┐         ┌─────────────────┐
│ Resample/Scale  │         │ Apply Filters   │
│ Image Data      │         │ (Smoothing)     │
└────────┬────────┘         └────────┬────────┘
         │                           │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Save Tile to Disk         │
         │ (data/tiles/zoom/x_y.png) │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Generate Next Tile        │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ All Tiles Complete        │
         │ for Zoom Level            │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Ready for Runtime Display │
         └───────────────────────────┘
```

---

## Data Loading & World Generation

### Procedural World Generation

```
┌──────────────────────────────────────────────────────────┐
│         Initialize World Economy                         │
└────────────────────┬─────────────────────────────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Load or Generate          │
         │ WorldSetupData            │
         └───────────┬───────────────┘
                     │
          ┌──────────┴─────────┐
          ↓                    ↓
    ┌──────────┐         ┌──────────┐
    │From JSON │         │Procedural│
    │File      │         │Generation│
    └────┬─────┘         └────┬─────┘
         │                    │
         └──────────┬─────────┘
                    │
                    ↓
         ┌───────────────────────────┐
         │ WorldDataGenerator        │
         │ .GenerateWorldData()      │
         └───────────┬───────────────┘
                     │
         ┌───────────┴────────────────┐
         ↓                            ↓
┌─────────────────┐         ┌─────────────────┐
│ Create Country  │         │ Select City     │
│ Data (Names,    │         │ Templates       │
│ Attributes)     │         │ (From Database) │
└────────┬────────┘         └────────┬────────┘
         │                           │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ ProceduralWorldGenerator  │
         │ .GenerateWorld()          │
         └───────────┬───────────────┘
                     │
         ┌───────────┴────────────────┐
         ↓                            ↓
┌─────────────────┐         ┌─────────────────┐
│ Create Entities:│         │ Create Economic │
│ - Countries     │         │ Actors:         │
│ - States        │         │ - Corporations  │
│ - Cities        │         │ - Construction  │
│ - Buildings     │         │   Companies     │
└────────┬────────┘         └────────┬────────┘
         │                           │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Initialize Factories      │
         │ (Based on Templates)      │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Setup Initial Stockpiles  │
         │ & Market Prices           │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Initialize Government     │
         │ Systems & Budgets         │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Return Populated World    │
         │ (Countries, Corporations) │
         └───────────────────────────┘
```

### Factory Blueprint Initialization

```
┌──────────────────────────────────────────────────────────┐
│      FactoryBlueprints.InitializeBlueprints()            │
└────────────────────┬─────────────────────────────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Define Good Definitions   │
         │ (Market.GoodDefinitions)  │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Create Goods:             │
         │ - Grain (RawMaterial)     │
         │ - Iron (RawMaterial)      │
         │ - Coal (RawMaterial)      │
         │ - Steel (IndustrialInput) │
         │ - Bread (ProcessedFood)   │
         │ - Tools (ConsumerProduct) │
         │ - etc.                    │
         └───────────┬───────────────┘
                     │
                     ↓
         ┌───────────────────────────┐
         │ Create Factory Blueprints:│
         └───────────┬───────────────┘
                     │
         ┌───────────┼────────────────┐
         ↓           ↓                ↓
┌─────────────┐ ┌─────────┐  ┌──────────────┐
│Farm         │ │Iron Mine│  │Steel Mill    │
│Input: -     │ │Input: - │  │Input: Iron,  │
│Output: Grain│ │Output:  │  │       Coal   │
│             │ │  Iron   │  │Output: Steel │
└─────────────┘ └─────────┘  └──────────────┘

         And many more blueprints...
                     │
                     ↓
         ┌───────────────────────────┐
         │ Store in AllBlueprints    │
         │ List                      │
         └───────────────────────────┘
```

---

## Summary

These flow charts illustrate the key processes in Economy Sim:

1. **Game Initialization** - From startup to playable state
2. **Production Cycle** - How factories produce goods
3. **Market & Trade** - Price discovery and trade execution
4. **Construction** - Building new factories
5. **Turn Processing** - Main game loop phases
6. **Player Actions** - How player input affects the game
7. **Map Rendering** - Displaying geographical data
8. **World Generation** - Creating the simulated world

Each process is interconnected, forming a complex simulation of economic activity.

---

**Related Documentation**:
- [Architecture Overview](ARCHITECTURE.md) - System design
- [Game Systems Guide](GAME_SYSTEMS.md) - Detailed mechanics
- [Data Model](DATA_MODEL.md) - Entity relationships
