# Data Model

This document describes the data structures and entity relationships in Economy Sim.

## Table of Contents
1. [Entity Relationship Diagram](#entity-relationship-diagram)
2. [Core Entities](#core-entities)
3. [Economic Entities](#economic-entities)
4. [Government Entities](#government-entities)
5. [Geographical Entities](#geographical-entities)
6. [Data Transfer Objects](#data-transfer-objects)
7. [Enumerations](#enumerations)

---

## Entity Relationship Diagram

### High-Level Entity Relationships

```
┌─────────────┐
│   Country   │
└──────┬──────┘
       │ 1:N
       ↓
┌─────────────┐
│    State    │
└──────┬──────┘
       │ 1:N
       ↓
┌─────────────┐         ┌──────────────┐
│    City     │←────────┤ Corporation  │
└──────┬──────┘  owns   └──────┬───────┘
       │ 1:N                   │ 1:N
       ↓                       ↓
┌─────────────┐         ┌──────────────┐
│   Factory   │◄────────┤  (ownership) │
└─────────────┘         └──────────────┘
       │
       │ produces/consumes
       ↓
┌─────────────┐
│    Good     │
└─────────────┘
       │
       │ traded on
       ↓
┌─────────────┐
│   Market    │
└─────────────┘
```

### Detailed Relationships

```
Country
  ├── Government (1:1)
  │     ├── PoliticalParty (1:N)
  │     ├── Policy (1:N)
  │     └── Law (1:N)
  ├── NationalFinancialSystem (1:1)
  │     ├── TaxPolicy (1:N)
  │     └── MonetaryPolicy (1:N)
  ├── State (1:N)
  │     ├── City (1:N)
  │     │     ├── Factory (1:N)
  │     │     │     ├── Good (inputs, 1:N)
  │     │     │     └── Good (outputs, 1:N)
  │     │     ├── Building (1:N)
  │     │     ├── ConstructionProject (1:N)
  │     │     └── Population (1:1)
  │     └── Resources (1:N)
  └── Resources (1:N)

Corporation
  ├── Factory (owns, 1:N)
  ├── Country (headquarters, N:1)
  └── CorporationSpecialization (1:1)

Market
  ├── Good (definitions, 1:N)
  ├── TradeOffer (1:N)
  └── TradeTransaction (history, 1:N)

ConstructionCompany
  ├── City (home, N:1)
  └── ConstructionProject (active, 1:N)
```

---

## Core Entities

### Country

**Purpose**: Top-level political and economic entity

**Properties**:
```csharp
public class Country
{
    // Identity
    string Name;                              // Country name
    
    // Political Structure
    List<State> States;                       // Regional divisions
    Government Government;                    // Political system
    
    // Economics
    double Budget;                            // National treasury
    int Population;                           // Total inhabitants
    decimal GDP;                              // Economic output
    double NationalExpenses;                  // Government spending
    Dictionary<string, double> Resources;     // Natural resources
    
    // Financial System
    NationalFinancialSystem FinancialSystem;  // Monetary system
}
```

**Relationships**:
- Contains multiple `State` objects (1:N)
- Has one `Government` (1:1)
- Has one `NationalFinancialSystem` (1:1)
- Referenced by `Corporation` for headquarters (N:1)

**Key Methods**:
```csharp
void AddResource(string resourceName, double amount)
bool RemoveResource(string resourceName, double amount)
double GetResourceAmount(string resourceName)
void DistributeFunds()  // Allocate budget to states
```

---

### State

**Purpose**: Regional subdivision of a country

**Properties**:
```csharp
public class State
{
    // Identity
    string Name;                          // State name
    
    // Geography
    List<City> Cities;                    // Cities in state
    
    // Economics
    double Budget;                        // State treasury
    int Population;                       // Total inhabitants
    double TaxRate;                       // State tax rate
    double StateExpenses;                 // State spending
    
    // Resources
    Dictionary<string, double> Resources; // State resources
}
```

**Relationships**:
- Belongs to one `Country` (N:1)
- Contains multiple `City` objects (1:N)

**Key Methods**:
```csharp
void AddResource(string resourceName, double amount)
bool RemoveResource(string resourceName, double amount)
void DistributeFunds()  // Allocate budget to cities
```

---

### City

**Purpose**: Local economic and population center

**Properties**:
```csharp
public class City
{
    // Identity
    string Name;                              // City name
    
    // Population & Economy
    int Population;                           // Inhabitants
    double Budget;                            // City treasury
    double TaxRate;                           // Local tax rate
    double CityExpenses;                      // City spending
    
    // Industry
    List<Factory> Factories;                  // Factories (thread-safe)
    
    // Commerce
    Dictionary<string, Good> Stockpile;       // Inventory
    Dictionary<string, double> LocalPrices;   // Market prices
    Dictionary<string, int> LocalSupply;      // Current supply
    Dictionary<string, int> LocalDemand;      // Current demand
}
```

**Relationships**:
- Belongs to one `State` (N:1)
- Contains multiple `Factory` objects (1:N)
- Has multiple `Building` objects (1:N)
- Has multiple `ConstructionProject` objects (1:N)

**Thread Safety**:
- `Factories` collection is protected with locks
- Use `AddFactory()`, `RemoveFactory()` methods for thread-safe access

**Key Methods**:
```csharp
void AddFactory(Factory factory)          // Thread-safe add
bool RemoveFactory(Factory factory)       // Thread-safe remove
int FactoryCount { get; }                 // Thread-safe count
List<Factory> Factories { get; }          // Returns snapshot
```

---

## Economic Entities

### Factory

**Purpose**: Production facility that converts inputs to outputs

**Properties**:
```csharp
public class Factory
{
    // Identity
    string Name;                              // Factory name
    
    // Ownership
    Corporation OwnerCorporation;             // Owner
    
    // Production
    List<Good> InputGoods;                    // Required inputs
    List<Good> OutputGoods;                   // Products
    int ProductionCapacity;                   // Max production/turn
    int BaseProductionCapacity;               // Original capacity
    
    // Labor
    int WorkersEmployed;                      // Total workers
    Dictionary<string, int> JobSlots;         // Required workers by type
    Dictionary<string, int> ActualEmployed;   // Actual workers by type
    
    // Infrastructure
    Building BuildingData;                    // Physical structure
}
```

**Relationships**:
- Owned by one `Corporation` (N:1)
- Located in one `City` (N:1)
- Consumes multiple `Good` objects (inputs, N:M)
- Produces multiple `Good` objects (outputs, N:M)
- Occupies one `Building` (1:1)

**Key Calculations**:
```csharp
// Production efficiency based on worker fulfillment
double efficiency = (double)WorkersEmployed / RequiredWorkers;
int actualOutput = (int)(ProductionCapacity * efficiency);
```

---

### Good

**Purpose**: Tradeable commodity

**Properties**:
```csharp
public class Good
{
    // Identity
    string Name;                  // Good identifier
    
    // Economics
    int Quantity;                 // Amount
    double BasePrice;             // Default price
    
    // Classification
    GoodCategory Category;        // Type of good
}
```

**Relationships**:
- Used by `Factory` (inputs/outputs, N:M)
- Stored in `City` stockpile (N:M)
- Traded on `Market` (N:M)
- Defined in `Market.GoodDefinitions` (registry)

**Categories**: See [Enumerations](#goodcategory)

---

### Corporation

**Purpose**: AI-controlled economic entity owning factories

**Properties**:
```csharp
public class Corporation
{
    // Identity
    string Name;                              // Corporation name
    
    // Business
    CorporationSpecialization Specialization; // Industry focus
    double Budget;                            // Corporate treasury
    
    // Assets
    List<Factory> OwnedFactories;             // Owned production
    
    // Location
    Country HeadquartersCountry;              // Home country
}
```

**Relationships**:
- Owns multiple `Factory` objects (1:N)
- Headquartered in one `Country` (N:1)

**Specializations**: See [Enumerations](#corporationspecialization)

---

### Market

**Purpose**: Global marketplace for goods

**Properties**:
```csharp
public static class Market
{
    // Good Registry
    static Dictionary<string, Good> GoodDefinitions;
    
    // Singleton instance for global market
    static GlobalMarket Instance;
}
```

**GlobalMarket Properties**:
```csharp
public class GlobalMarket
{
    // Trading
    List<TradeOffer> ActiveOffers;            // Current listings
    List<TradeTransaction> TransactionHistory; // Past trades
    
    // Statistics
    Dictionary<string, double> GlobalPrices;   // Average prices
    Dictionary<string, int> TotalSupply;       // Total available
    Dictionary<string, int> TotalDemand;       // Total wanted
}
```

---

### FactoryBlueprint

**Purpose**: Template for creating factories

**Properties**:
```csharp
public class FactoryBlueprint
{
    // Identity
    string FactoryTypeName;                   // Type identifier
    
    // Production
    Good OutputGood;                          // What it produces
    List<Good> InputGoods;                    // What it needs
    GoodCategory ProducedGoodCategory;        // Output category
    
    // Labor
    Dictionary<string, double> DefaultJobSlotDistribution; // Worker types
}
```

**Usage**:
```csharp
// Create factory from blueprint
var blueprint = FactoryBlueprints.AllBlueprints
    .First(b => b.FactoryTypeName == "Steel Mill");
var factory = new Factory("New Steel Mill", 100);
factory.InputGoods = blueprint.InputGoods;
factory.OutputGoods = new List<Good> { blueprint.OutputGood };
```

---

## Government Entities

### Government

**Purpose**: Political system managing a country

**Properties**:
```csharp
public class Government
{
    // Political
    List<PoliticalParty> Parties;             // Political parties
    
    // Governance
    Dictionary<string, Policy> Policies;      // Active policies
    List<Law> Laws;                          // Enacted laws
}
```

**Relationships**:
- Belongs to one `Country` (1:1)
- Contains multiple `PoliticalParty` objects (1:N)
- Contains multiple `Policy` objects (1:N)
- Contains multiple `Law` objects (1:N)

---

### PoliticalParty

**Purpose**: Political organization

**Properties**:
```csharp
public class PoliticalParty
{
    // Identity
    string Name;                              // Party name
    
    // Power
    double ShareOfGovernment;                 // Control (0.0-1.0)
    
    // Ideology
    Dictionary<string, double> PolicyPositions; // Stance on issues
}
```

---

### NationalFinancialSystem

**Purpose**: National monetary and fiscal system

**Properties**:
```csharp
public class NationalFinancialSystem
{
    // Identity
    string CountryName;                       // Associated country
    
    // Finance
    decimal Treasury;                         // National reserves
    decimal DebtLevel;                        // Outstanding debt
    CurrencyStandard Standard;                // Monetary standard
    
    // Policies
    List<TaxPolicy> TaxPolicies;             // Tax structures
    List<MonetaryPolicy> MonetaryPolicies;   // Fiscal policies
}
```

**Relationships**:
- Belongs to one `Country` (1:1)
- Contains multiple `TaxPolicy` objects (1:N)
- Contains multiple `MonetaryPolicy` objects (1:N)

---

### TaxPolicy

**Purpose**: Taxation rules

**Properties**:
```csharp
public class TaxPolicy
{
    // Identity
    string PolicyName;                        // Tax name
    
    // Rate
    decimal Rate;                             // Tax percentage
    
    // Application
    string AppliesToSector;                   // Industry sector
    string AppliesToPopGroup;                 // Social class
}
```

---

## Geographical Entities

### Building

**Purpose**: Physical structure in a city

**Properties**:
```csharp
public class Building
{
    // Identity
    string BuildingType;                      // Type (Residential, etc.)
    
    // Capacity
    int Capacity;                             // Usage capacity
    
    // Location
    string Address;                           // Location identifier
}
```

**Types**:
- Residential
- Commercial
- Industrial
- Government
- Mixed-Use

---

### Parcel

**Purpose**: Land plot with boundaries

**Properties**:
```csharp
public class Parcel
{
    // Identity
    int ParcelId;                             // Unique ID
    
    // Geography
    Geometry Shape;                           // Boundary polygon
    double Area;                              // Size
    
    // Ownership
    string Owner;                             // Owner identifier
    
    // Usage
    string ZoningType;                        // Land use type
}
```

---

## Data Transfer Objects

### WorldSetupData

**Purpose**: Root container for world data

**Properties**:
```csharp
public class WorldSetupData
{
    List<CountryData> Countries;              // All countries
    List<ConstructionCompanyData> ConstructionCompanies; // Builders
}
```

**Usage**: Loaded from JSON or generated procedurally

---

### CountryData

**Purpose**: DTO for country initialization

**Properties**:
```csharp
public class CountryData
{
    string Name;                              // Country name
    double TaxRate;                           // National tax
    double NationalExpenses;                  // Spending
    int InitialPopulation;                    // Starting population
    double InitialBudget;                     // Starting money
    List<StateData> States;                   // State data
    bool IsPlayerControlled;                  // Player's nation
}
```

---

### StateData

**Purpose**: DTO for state initialization

**Properties**:
```csharp
public class StateData
{
    string Name;                              // State name
    double TaxRate;                           // State tax
    double StateExpenses;                     // Spending
    int InitialPopulation;                    // Starting population
    double InitialBudget;                     // Starting money
    List<CityData> Cities;                    // City data
}
```

---

### CityData

**Purpose**: DTO for city initialization

**Properties**:
```csharp
public class CityData
{
    string Name;                              // City name
    int InitialPopulation;                    // Starting population
    double InitialBudget;                     // Starting money
    double TaxRate;                           // City tax
    double CityExpenses;                      // Spending
    List<InitialFactoryData> InitialFactories; // Starting factories
}
```

---

### InitialFactoryData

**Purpose**: DTO for initial factory setup

**Properties**:
```csharp
public class InitialFactoryData
{
    string FactoryTypeName;                   // Blueprint reference
    int Capacity;                             // Production capacity
}
```

---

### ConstructionCompanyData

**Purpose**: DTO for construction company

**Properties**:
```csharp
public class ConstructionCompanyData
{
    string Name;                              // Company name
    string HomeCity;                          // Base location
    double InitialBudget;                     // Starting capital
    int Workers;                              // Workforce size
}
```

---

## Enumerations

### GoodCategory

**Purpose**: Classify types of goods

```csharp
public enum GoodCategory
{
    RawMaterial,      // Unprocessed resources (Grain, Iron, Coal)
    IndustrialInput,  // Processed materials (Steel, Chemicals)
    ProcessedFood,    // Prepared food (Bread, Preserved goods)
    ConsumerProduct,  // Consumer goods (Cloth, Furniture, Tools)
    CapitalGood       // Industrial equipment (Machinery, Tools)
}
```

**Usage**:
- Determines market behavior
- Affects pricing algorithms
- Influences trade patterns

---

### CorporationSpecialization

**Purpose**: Define corporation industry focus

```csharp
public enum CorporationSpecialization
{
    None,           // No specialization (startup)
    Agriculture,    // Food production
    Mining,         // Resource extraction
    HeavyIndustry,  // Steel, machinery, construction
    LightIndustry,  // Consumer goods, textiles
    Diversified     // Multiple sectors
}
```

**Usage**:
- Guides AI factory building decisions
- Affects corporate strategy
- Influences investment priorities

---

### CurrencyStandard

**Purpose**: Define monetary system backing

```csharp
public enum CurrencyStandard
{
    Gold,    // Gold-backed currency
    Silver,  // Silver-backed currency
    Fiat     // Government-backed currency
}
```

**Usage**:
- Determines currency stability
- Affects inflation rates
- Influences international trade

---

## Data Flow

### Creation Flow

```
1. WorldSetupData (JSON or Generated)
     ↓
2. CountryData, StateData, CityData (DTOs)
     ↓
3. ProceduralWorldGenerator.GenerateWorld()
     ↓
4. Country, State, City (Entities)
     ↓
5. Factories created from Blueprints
     ↓
6. Corporations assigned ownership
     ↓
7. World ready for simulation
```

### Runtime Data Updates

```
Each Turn:
  1. Factories produce → Update Good quantities
  2. Markets calculate → Update Prices
  3. Trade executes → Transfer Goods & Money
  4. Governments collect → Update Budgets
  5. UI reads data → Display to player
```

---

## Data Persistence

### Saved Data
- Country/State/City entities
- Factory states
- Corporation budgets
- Market prices
- Player role and controlled entities

### Not Saved (Regenerated)
- UI state
- Temporary calculations
- Cached data
- Rendering buffers

---

## Data Validation

### Key Constraints

**Money/Budget**:
- Must be non-negative (or handle debt explicitly)
- Transactions must balance (money in = money out)

**Population**:
- Must be non-negative
- Employment ≤ Population
- City population = Sum of worker classes

**Goods**:
- Quantities must be non-negative
- Production cannot exceed capacity
- Consumption cannot exceed stockpile

**Relationships**:
- Every Factory must have an owner (Corporation)
- Every City must belong to a State
- Every State must belong to a Country

---

**Related Documentation**:
- [Architecture Overview](ARCHITECTURE.md) - System design
- [Game Systems Guide](GAME_SYSTEMS.md) - How entities interact
- [Code Organization](CODE_ORGANIZATION.md) - Where entities are defined
