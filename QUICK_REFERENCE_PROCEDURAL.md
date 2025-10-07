# Quick Reference: Procedural World Generation

## Quick Start

### Generate a World
```csharp
// Default settings (10 countries, 5 states each, ~3 cities per state)
var (countries, corporations) = Economy.InitializeWorldEconomy();

// Custom settings
var (countries, corporations) = Economy.InitializeWorldEconomy(
    numCountries: 15,
    numStatesPerCountry: 4,
    numCitiesPerState: 5,
    seed: 12345  // Optional: null for random
);
```

### Integration in GameView
```csharp
// Replace InitializeEconomyData() with:
private void InitializeEconomyData()
{
    if (_economyInitialized) return;

    var (countries, corporations) = Economy.InitializeWorldEconomy();
    
    _allCountries = countries;
    _allCorporations = corporations;
    _currentCountry = countries.FirstOrDefault();
    _playerCountry = _currentCountry;
    
    RegisterEconomyCityAnchors();
    _playerRoleManager = new PlayerRoleManager();
    _playerRoleManager.AssumeRolePrimeMinister(_currentCountry);
    InitializeTradeSystems();
    
    _economyInitialized = true;
}
```

## City Types at a Glance

| Type | Population | Key Feature | Example Factories |
|------|-----------|-------------|------------------|
| **Farming** | 50k-500k | Agricultural | Grain Farm, Bakery, Cattle Ranch |
| **Mining** | 100k-800k | Resource extraction | Coal Mine, Iron Mine, Copper Mine |
| **Manufacturing** | 200k-2M | Industrial processing | Steel Mill, Textile Mill, Tool Factory |
| **Trading** | 150k-1.5M | Commercial hub | Printing Press, Luxury Tailor, Furniture |
| **Fishing** | 80k-600k | Coastal food | Fishing Wharf, Cannery, Salt Mine |
| **MixedIndustrial** | 180k-1.2M | Balanced | Steel Mill, Bakery, Sawmill |
| **TechHub** | 150k-1.8M | Advanced tech | Machine Parts, Electronics, Chemicals |
| **PortCity** | 250k-2.5M | Major port | Sawmill, Steel Mill, Luxury Goods |
| **CapitalCity** | 500k-5M | Government center | Books, Furniture, Automobiles |

## Template Probabilities

Default weights for city type selection:
- MixedIndustrial: 3.0 (most common)
- Farming: 2.5
- Manufacturing: 2.5
- Mining: 2.0
- Trading: 2.0
- Fishing: 1.5
- Agricultural: 1.5
- PortCity: 1.5
- TechHub: 1.0
- CapitalCity: 0.5 (rare, auto-assigned to first city)

## Population Distribution Examples

### Farming City
- Laborers: 60%
- Craftsmen: 25%
- Engineers: 8%
- Managers: 5%
- Clerks: 2%

### Tech Hub
- Engineers: 30%
- Craftsmen: 25%
- Laborers: 25%
- Managers: 12%
- Clerks: 8%

### Capital City
- Laborers: 30%
- Craftsmen: 25%
- Engineers: 18%
- Managers: 17%
- Clerks: 10%

## Economic Multipliers

| City Type | Budget | Expenses | Income |
|-----------|--------|----------|--------|
| Farming | 0.8x | 0.7x | 0.9x |
| Mining | 1.2x | 1.1x | 1.3x |
| Manufacturing | 1.5x | 1.3x | 1.6x |
| Trading | 1.8x | 1.5x | 1.7x |
| TechHub | 2.0x | 1.7x | 2.2x |
| PortCity | 2.2x | 1.8x | 2.0x |
| CapitalCity | 2.5x | 2.0x | 2.3x |

## Common Queries

### Find All Mining Cities
```csharp
var miningCities = countries
    .SelectMany(c => c.States)
    .SelectMany(s => s.Cities)
    .Where(city => city.Factories.Count(f => 
        f.Name.Contains("Mine") || f.Name.Contains("Quarry")) >= 2);
```

### Find Largest Cities
```csharp
var largest = countries
    .SelectMany(c => c.States)
    .SelectMany(s => s.Cities)
    .OrderByDescending(city => city.Population)
    .Take(10);
```

### Count Corporations by Type
```csharp
var corpsByType = corporations
    .GroupBy(c => c.Specialization)
    .OrderByDescending(g => g.Count());
```

## Adding a New City Type

1. Add to `CityType` enum in `CityTemplates.cs`
2. Add template in `CityTemplateManager.InitializeTemplates()`:

```csharp
_templates[CityType.YourNewType] = new CityTemplate
{
    Type = CityType.YourNewType,
    Description = "Your description",
    MinPopulation = 100000,
    MaxPopulation = 1000000,
    BudgetMultiplier = 1.5,
    ExpenseMultiplier = 1.2,
    IncomeMultiplier = 1.4,
    PopulationDistribution = new Dictionary<string, double>
    {
        { "Laborers", 0.40 },
        { "Craftsmen", 0.30 },
        { "Engineers", 0.20 },
        { "Managers", 0.07 },
        { "Clerks", 0.03 }
    },
    FactoryWeights = new Dictionary<string, double>
    {
        { "Factory Type 1", 3.0 },
        { "Factory Type 2", 2.0 }
    },
    StockpileBias = new Dictionary<string, int>
    {
        { "Good Name", 5000 }
    }
};
```

3. Add weight in `GetWeightedRandomCityType()`

## Troubleshooting

### No factories generated
? Ensure `FactoryBlueprints.InitializeBlueprints()` is called first

### All cities same type
? Use `GetWeightedRandomCityType()` not `GetRandomCityType()`

### Population distribution wrong
? Check template PopulationDistribution sums to ~1.0

### Corporations not created
? Verify corporationPool is passed through generation chain

## Performance Tips

- Small world (5 countries): ~50ms
- Medium world (10 countries): ~100ms
- Large world (20 countries): ~200ms

Use `seed` parameter for reproducible worlds during testing.

## Files Reference

- **CityTemplates.cs** - Template definitions
- **ProceduralWorldGenerator.cs** - Generation logic
- **Economy.cs** - `InitializeWorldEconomy()` entry point
- **WorldDataGenerator.cs** - Country/state name templates
- **Examples/WorldGenerationExample.cs** - Usage examples

## Key Benefits

? No hardcoded data  
? Unique every playthrough  
? Realistic city specialization  
? Easy to customize  
? Fast generation  
? Emergent gameplay  

---

**Need more details?** See `PROCEDURAL_GENERATION_GUIDE.md` for comprehensive documentation.
