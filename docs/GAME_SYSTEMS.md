# Game Systems Guide

This document provides detailed explanations of all major game systems in Economy Sim.

## Table of Contents
1. [Economy System](#economy-system)
2. [Production System](#production-system)
3. [Market & Trade System](#market--trade-system)
4. [Government System](#government-system)
5. [Population System](#population-system)
6. [Construction System](#construction-system)
7. [Corporation System](#corporation-system)
8. [Map & Rendering System](#map--rendering-system)
9. [Player Role System](#player-role-system)

---

## Economy System

### Overview
The economy system is the heart of the simulation, managing the flow of goods, money, and resources throughout the game world.

### Key Components

#### Goods (`Good` class)
- **Properties**:
  - `Name` - Identifier (e.g., "Grain", "Steel")
  - `Quantity` - Amount available
  - `BasePrice` - Default market price
  - `Category` - Type classification (RawMaterial, ProcessedFood, etc.)

#### Good Categories
```csharp
public enum GoodCategory
{
    RawMaterial,      // Grain, Coal, Iron
    IndustrialInput,  // Steel, processed materials
    ProcessedFood,    // Bread, preserved foods
    ConsumerProduct,  // Cloth, Furniture, Tools
    CapitalGood       // Industrial equipment
}
```

#### Market (`Market` class)
- Maintains `GoodDefinitions` - Registry of all tradeable goods
- Tracks supply and demand globally
- Calculates price fluctuations
- Manages transaction history

### Economic Flow
1. **Production** - Factories produce goods
2. **Distribution** - Goods added to city stockpiles
3. **Consumption** - Population and factories consume goods
4. **Trade** - Excess sold, shortages imported
5. **Pricing** - Prices adjust based on supply/demand

### Key Mechanics

#### Price Discovery
- Prices increase when demand exceeds supply
- Prices decrease when supply exceeds demand
- Base price acts as an anchor
- Price changes are gradual (typically ±5% per turn)

#### Supply & Demand Calculation
```
Supply = City Stockpile + Current Turn Production
Demand = Population Consumption + Factory Input Requirements
Ratio = Supply / Demand

If Ratio < 0.8: Price increases
If Ratio > 1.2: Price decreases
Otherwise: Price stable
```

---

## Production System

### Overview
Factories are the primary producers of goods, converting input materials into output products.

### Factory Structure

#### Factory Components
```csharp
public class Factory
{
    string Name;                    // Factory identifier
    Corporation OwnerCorporation;   // Who owns this factory
    List<Good> InputGoods;          // Required inputs
    List<Good> OutputGoods;         // Products created
    int ProductionCapacity;         // Max units per turn
    int WorkersEmployed;            // Current workforce
    Dictionary<string, int> JobSlots;     // Job type requirements
    Dictionary<string, int> ActualEmployed; // Filled positions
}
```

#### Factory Blueprints
Blueprints define factory types:
```csharp
public class FactoryBlueprint
{
    string FactoryTypeName;                // "Steel Mill", "Farm", etc.
    Good OutputGood;                       // What it produces
    List<Good> InputGoods;                 // What it needs
    GoodCategory ProducedGoodCategory;     // Category of output
    Dictionary<string, double> DefaultJobSlotDistribution; // Worker types
}
```

### Production Chains

Common production chains:

**Steel Production Chain**:
```
Iron Mine → Iron Ore
     +
Coal Mine → Coal
     ↓
Steel Mill → Steel → Tool Factory → Tools
                  → Construction Materials
```

**Food Production Chain**:
```
Farm → Grain → Mill → Flour → Bakery → Bread
```

**Textile Chain**:
```
Cotton Farm → Cotton → Textile Mill → Cloth → Tailor → Clothes
```

### Production Process

#### Each Turn Per Factory:
1. **Check Workers** - Ensure factory has sufficient labor
2. **Check Inputs** - Verify input goods available in city stockpile
3. **Calculate Efficiency** - Based on worker fulfillment ratio
4. **Consume Inputs** - Remove required inputs from stockpile
5. **Produce Outputs** - Create goods (capacity × efficiency)
6. **Add to Stockpile** - Output goods added to city
7. **Pay Workers** - Wages distributed
8. **Corporation Profit** - Remainder goes to owner

#### Production Formula
```
Output Quantity = Base Capacity × Worker Efficiency

Worker Efficiency = (Actual Workers / Required Workers)
                    Capped at 1.0 (100%)
```

### Job Types
Factories employ different worker types:
- **Laborers** (50-70%) - Unskilled manual labor
- **Craftsmen** (15-25%) - Skilled workers
- **Engineers** (10-15%) - Technical specialists
- **Managers** (5-10%) - Administrative staff

Different factory types have different distributions based on complexity.

---

## Market & Trade System

### Local Markets

#### City-Level Markets
Each city maintains:
- **Stockpile** - Current inventory of all goods
- **Local Prices** - City-specific prices based on local supply/demand
- **Local Supply** - Production this turn
- **Local Demand** - Consumption this turn

#### Market Operations
```csharp
// Price update logic
foreach (var good in Market.GoodDefinitions)
{
    double supply = city.LocalSupply[good.Name];
    double demand = city.LocalDemand[good.Name];
    double ratio = supply / demand;
    
    if (ratio < 0.8)
        city.LocalPrices[good.Name] *= 1.05; // +5%
    else if (ratio > 1.2)
        city.LocalPrices[good.Name] *= 0.95; // -5%
}
```

### Global Market

#### International Trade (`GlobalMarket` class)
- **Purpose**: Enable trade between countries
- **Mechanism**: Centralized marketplace
- **Participants**: Corporations, countries

#### Trade Offers
```csharp
public class TradeOffer
{
    string SellerName;
    string GoodName;
    int Quantity;
    double PricePerUnit;
    DateTime ListedDate;
}
```

#### Trade Execution Process
1. Seller lists goods on global market
2. Buyer searches for desired goods
3. If price acceptable, buyer purchases
4. Goods transferred to buyer's stockpile
5. Payment transferred to seller
6. Transaction logged

### Trade Routes

#### Established Routes (`TradeRoute` class)
- **Definition**: Regular trade agreements between cities/countries
- **Benefits**: Reduced costs, guaranteed supply
- **Management**: Can be established, modified, cancelled

#### Route Properties
```csharp
public class TradeRoute
{
    City OriginCity;
    City DestinationCity;
    string GoodName;
    int QuantityPerTurn;
    double AgreedPrice;
    bool IsActive;
}
```

### Enhanced Trade Features

#### Trade Proposals
- **Bilateral agreements** between nations
- **Terms**: Goods, quantities, prices, duration
- **Approval**: Both parties must accept

#### Trade Statistics
- **Volume**: Total goods traded
- **Value**: Monetary value of trade
- **Balance**: Import/export balance per country
- **Top Partners**: Most frequent trade partners

---

## Government System

### Overview
Each country has a government that manages policies, budgets, and national affairs.

### Government Structure

#### Government Components
```csharp
public class Government
{
    List<PoliticalParty> Parties;     // Political parties
    Dictionary<string, Policy> Policies; // Active policies
    List<Law> Laws;                   // Enacted laws
}
```

#### Political Parties
```csharp
public class PoliticalParty
{
    string Name;
    double ShareOfGovernment;  // Percentage (0.0 to 1.0)
    Dictionary<string, double> PolicyPositions;
}
```

### National Financial System

#### Financial System Structure
```csharp
public class NationalFinancialSystem
{
    string CountryName;
    decimal Treasury;              // National reserves
    decimal DebtLevel;             // Outstanding debt
    CurrencyStandard Standard;     // Gold, Silver, Fiat
    List<TaxPolicy> TaxPolicies;   // Tax structures
    List<MonetaryPolicy> MonetaryPolicies; // Fiscal policies
}
```

#### Tax System
- **Income Tax** - Tax on worker wages
- **Corporate Tax** - Tax on factory profits
- **Sales Tax** - Tax on goods transactions
- **Property Tax** - Tax on buildings/land

#### Tax Rates by Level
```
Country Level: National tax rate
  ↓
State Level: State tax rate (added to national)
  ↓
City Level: Local tax rate (added to above)
  ↓
Total Tax = National + State + City
```

### Budget Management

#### Revenue Sources
1. **Taxes** - Primary income
2. **Tariffs** - Import/export duties
3. **State Enterprises** - Government-owned factories
4. **Bonds** - Borrowing

#### Expenditures
1. **Infrastructure** - Roads, utilities
2. **Military** - Defense spending
3. **Administration** - Government operations
4. **Social Programs** - Welfare, education
5. **Subsidies** - Support for industries

### Policies

#### Policy Types
- **Economic Policies** - Trade restrictions, subsidies
- **Social Policies** - Education, healthcare
- **Industrial Policies** - Sector support
- **Monetary Policies** - Interest rates, money supply

#### Policy Effects
Policies can affect:
- Tax rates
- Production efficiency
- Population happiness
- Trade volumes
- Immigration/emigration

---

## Population System

### Overview
Population represents the workforce and consumers in the economy.

### Population Structure

#### Social Classes
- **Aristocrats** (1-5%) - Wealthy elite, high consumption
- **Capitalists** (5-10%) - Business owners, factory owners
- **Middle Class** (15-25%) - Educated professionals
- **Craftsmen** (20-30%) - Skilled workers
- **Laborers** (40-60%) - Unskilled workers

#### Population Properties
```csharp
// Per city
int Population;                    // Total inhabitants
Dictionary<string, int> PopByClass; // Class distribution
```

### Employment

#### Labor Market
- Factories require workers
- Workers drawn from population
- Wages paid per worker type
- Unemployment when insufficient jobs

#### Worker Assignment
```csharp
// Simplified labor allocation
foreach (var factory in city.Factories)
{
    foreach (var jobType in factory.JobSlots)
    {
        int needed = factory.JobSlots[jobType];
        int available = city.GetAvailableWorkers(jobType);
        int hired = Math.Min(needed, available);
        factory.ActualEmployed[jobType] = hired;
    }
}
```

### Consumption

#### Consumption Patterns
Each social class has different needs:

**Laborers**:
- Bread (high)
- Cheap clothing (medium)
- Basic tools (low)

**Craftsmen**:
- Varied food (medium-high)
- Quality clothing (medium)
- Tools (medium)
- Furniture (low)

**Capitalists**:
- Luxury food (high)
- Luxury clothing (high)
- Books, art (medium)
- Luxury furniture (medium)

#### Consumption Calculation
```
Total Demand = Σ (Population[class] × ConsumptionRate[class][good])
```

### Population Growth
- **Birth Rate** - Natural increase
- **Death Rate** - Natural decrease
- **Migration** - Movement between cities/countries
- **Net Growth** = Births - Deaths + Net Migration

---

## Construction System

### Overview
The construction system handles building new factories and infrastructure.

### Construction Companies

#### Company Structure
```csharp
public class ConstructionCompany
{
    string Name;
    City HomeCity;
    double Budget;
    int Workers;
    List<ConstructionProject> ActiveProjects;
}
```

#### Construction Process
1. **Request** - Player/Corporation requests construction
2. **Quote** - Company provides cost estimate
3. **Contract** - If accepted, project begins
4. **Construction** - Progress over multiple turns
5. **Completion** - Factory becomes operational

### Construction Projects

#### Project Structure
```csharp
public class ConstructionProject
{
    string ProjectName;
    City Location;
    FactoryBlueprint Blueprint;
    double TotalCost;
    double ProgressPercentage;
    int RemainingTurns;
    Corporation Client;
}
```

#### Project Duration
```
Duration = Base Duration × Size Factor × Complexity Factor

Size Factor = Factory Capacity / 100
Complexity Factor based on factory type:
  - Simple (Farm): 1.0
  - Medium (Textile Mill): 1.5
  - Complex (Steel Mill): 2.0
```

### Procedural City Building

#### ProceduralCityBuilder
Generates city layouts and initial infrastructure:
- **Residential Areas** - Housing for population
- **Industrial Zones** - Factory locations
- **Commercial Districts** - Shops, markets
- **Infrastructure** - Roads, utilities

#### Building Types
```csharp
public class Building
{
    string BuildingType;  // Residential, Commercial, Industrial
    int Capacity;         // People housed or businesses
    string Address;       // Location identifier
}
```

---

## Corporation System

### Overview
Corporations are AI-controlled entities that own and operate factories.

### Corporation Structure

#### Corporation Properties
```csharp
public class Corporation
{
    string Name;
    CorporationSpecialization Specialization;
    double Budget;
    List<Factory> OwnedFactories;
    Country HeadquartersCountry;
}
```

#### Specializations
```csharp
public enum CorporationSpecialization
{
    None,           // No specialization
    Agriculture,    // Food production
    Mining,         // Resource extraction
    HeavyIndustry,  // Steel, machinery
    LightIndustry,  // Consumer goods
    Diversified     // Multiple sectors
}
```

### AI Behavior

#### Decision Making
Corporations make decisions based on:
1. **Profitability** - Which factories make money
2. **Market Demand** - What goods are needed
3. **Competition** - What others are producing
4. **Resources** - Available capital

#### Actions
- **Build Factories** - Expand production capacity
- **Close Factories** - Shut down unprofitable operations
- **Adjust Production** - Change output quantities
- **Trade** - Buy inputs, sell outputs
- **Invest** - Upgrade existing facilities

### Corporate Finance

#### Revenue Streams
- **Factory Profits** - Primary income
- **Trade Profits** - Buy low, sell high
- **Dividends** - From owned subsidiaries

#### Expenses
- **Factory Operating Costs** - Workers, maintenance
- **Construction Costs** - New factories
- **Input Purchases** - Raw materials
- **Taxes** - Corporate taxes to government

---

## Map & Rendering System

### Overview
The map system visualizes the game world using real geographical data.

### Map Data Sources

#### Geographical Data
- **ETOPO1** - Elevation and terrain data
- **Natural Earth** - Country/state boundaries
- **Custom Shapefiles** - Political borders

#### Data Formats
- **GeoTIFF** - Raster elevation data
- **Shapefiles** - Vector political boundaries
- **JSON** - Custom game data

### Multi-Resolution System

#### Zoom Levels
The map supports multiple zoom levels:
- **Level 0** - World view (continent scale)
- **Level 1** - Regional view (country scale)
- **Level 2** - Local view (state scale)
- **Level 3** - City view (detailed)

#### Tile System
```
Tiles are pre-generated at each zoom level:
  data/tiles/
    zoom0/
      tile_0_0.png
      tile_0_1.png
      ...
    zoom1/
      tile_0_0.png
      ...
```

### Rendering Layers

#### Layer Stack (bottom to top)
1. **Base Terrain** - Elevation/color
2. **Political Borders** - Country/state lines
3. **Population Density** - Heat map overlay
4. **Cities** - City markers
5. **Selection** - Highlighted entities
6. **UI Overlays** - Labels, tooltips

### Map Managers

#### MultiResolutionMapManager
- Manages tiles at different zoom levels
- Assembles view from appropriate tiles
- Handles tile caching

#### HybridMapManager
- Combines raster and vector data
- Renders political borders
- Overlays game data on geographic base

#### PoliticalBorderManager
- Loads country/state boundary data
- Renders political divisions
- Handles border detection (which country is at coordinate)

### Rendering Pipeline

#### Frame Rendering
```csharp
1. Calculate viewport bounds (lat/lon)
2. Determine appropriate zoom level
3. Load required tiles
4. Composite layers
5. Render to SkiaSharp canvas (SKBitmap)
6. Convert to Avalonia Image
7. Display in UI
```

---

## Player Role System

### Overview
The player can assume different roles to control various aspects of the game.

### Available Roles

#### Prime Minister
- **Control**: Entire country
- **Powers**:
  - Set national policies
  - Manage national budget
  - International diplomacy
  - Declare war/peace
  - Set tariffs and trade policy

#### Governor
- **Control**: Single state
- **Powers**:
  - Set state policies
  - Manage state budget
  - Subsidize industries
  - Regulate local trade

#### CEO (Corporation)
- **Control**: Single corporation
- **Powers**:
  - Build factories
  - Set production quotas
  - Buy/sell goods
  - Manage corporate budget
  - Hire/fire workers

### Role Switching

#### Changing Roles
```csharp
// Example role change
PlayerRoleManager.AssumeRolePrimeMinister(selectedCountry);
PlayerRoleManager.AssumeRoleGovernor(selectedState);
PlayerRoleManager.AssumeRoleCEO(selectedCorporation);
```

#### Restrictions
- Can only control one entity at a time
- Some actions only available in specific roles
- Role changes may have cooldown periods (game design choice)

### Player Actions by Role

#### As Prime Minister
```
Available Actions:
  - Edit Policy → PolicyManagerWindow
  - View Country Statistics → StatsWindow
  - Diplomatic Relations → DiplomaticRelationsWindow
  - International Trade Agreements
  - National Budget Management
```

#### As Governor
```
Available Actions:
  - State Policy Adjustments
  - Local Infrastructure Projects
  - State Budget Allocation
  - Inter-state Trade
```

#### As CEO
```
Available Actions:
  - Build Factory → ConstructionWindow
  - View Factory Stats → FactoryStatsWindow
  - Trade Goods → TradeProposalWindow
  - Adjust Production Levels
  - Manage Workforce
```

---

## System Integration

### How Systems Work Together

```
Player Action (Build Factory as CEO)
    ↓
Construction System creates project
    ↓
Over multiple turns, project progresses
    ↓
Factory completed and added to City
    ↓
Production System includes new factory
    ↓
Factory produces goods each turn
    ↓
Market System adjusts prices based on new supply
    ↓
Corporation System calculates profit
    ↓
Government System collects taxes on profit
    ↓
UI updates to show changes
```

### Data Dependencies

```
Country
  └── States
        └── Cities
              ├── Factories (owned by Corporations)
              │     └── Production affects Markets
              ├── Population (workers & consumers)
              │     └── Consumption affects Markets
              └── Buildings (infrastructure)

Markets affect:
  - Factory profitability
  - Population satisfaction
  - Government tax revenue
  - Trade volumes
```

---

**Related Documentation**:
- [Architecture Overview](ARCHITECTURE.md) - System architecture
- [Flow Charts](FLOWCHARTS.md) - Visual process flows
- [Data Model](DATA_MODEL.md) - Entity relationships
