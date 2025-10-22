# Economy Generation Fix

## Problem
The economy generation logic in `GameView.axaml.cs` was broken due to several issues:

1. **Missing Method**: Code referenced a non-existent `GetAllCountriesFromMap()` method
2. **Unreachable Code**: The fallback procedural generation code was unreachable due to incorrect control flow
3. **Confusing Logic**: The code had multiple nested if-else blocks that made the logic difficult to follow

## Root Cause
The `InitializeEconomyData()` method was trying to generate the economy from map data, but the implementation had several flaws:

```csharp
// OLD CODE - BROKEN
if (mapStates != null && mapStates.Count > 0)
{
    var mapCountries = GetAllCountriesFromMap(); // METHOD DOESN'T EXIST!
    var (countries, corporations) = Economy.GenerateWorldEconomyFromMapData(mapCountries, mapStates);
    // ...
}
else
{
    // Fallback code that was never reached
    var (countries, corporations) = Economy.InitializeWorldEconomy();
    // ...
}
```

## Solution
Rewrote the `InitializeEconomyData()` method with clear, working logic:

### 1. **Extract Country Data from States**
Instead of calling a non-existent method, we now extract country information directly from the state data:

```csharp
// Get all unique countries from the states
var countryNames = mapStates
    .Where(s => !string.IsNullOrWhiteSpace(s.CountryName))
    .Select(s => s.CountryName)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();

// Build country features list from state data
var mapCountries = new List<IndexedCountryFeature>();
foreach (var countryName in countryNames)
{
    var firstState = mapStates.First(s => 
        s.CountryName.Equals(countryName, StringComparison.OrdinalIgnoreCase));
    
    mapCountries.Add(new IndexedCountryFeature
    {
        CountryName = countryName,
        CountryCode = firstState.CountryCode ?? countryName.Substring(0, Math.Min(3, countryName.Length)).ToUpper(),
        RasterCode = firstState.RasterCode
    });
}
```

### 2. **Proper Fallback Logic**
Used a boolean flag to track whether map data was successfully used:

```csharp
bool useMapData = false;

if (_mapManager != null)
{
    var mapStates = _mapManager.GetAllStates();
    
    if (mapStates != null && mapStates.Count > 0)
    {
        // Extract countries and generate from map data
        var (countries, corporations) = Economy.GenerateWorldEconomyFromMapData(mapCountries, mapStates);
        // ...
        useMapData = true;
    }
}

// Fall back to procedural generation if map data not available
if (!useMapData)
{
    var (countries, corporations) = Economy.InitializeWorldEconomy();
    // ...
}
```

### 3. **Better Logging**
Added comprehensive debug logging to track the generation process:

```csharp
Debug.WriteLine($"[Economy Init] Found {mapStates?.Count ?? 0} states from map");
Debug.WriteLine($"[Economy Init] Found {countryNames.Count} unique countries from states");
Debug.WriteLine($"[Economy Init] Using map data: {mapCountries.Count} countries, {mapStates.Count} states");
// OR
Debug.WriteLine($"[Economy Init] No map data available, using procedural generation");
```

## Benefits

### ? **Works Correctly**
- Economy generation now works whether map data is available or not
- Proper fallback to procedural generation

### ? **Clear Logic Flow**
- Easy to understand and maintain
- Single responsibility for each code section

### ? **Better Debugging**
- Comprehensive logging at each step
- Easy to track what's happening during generation

### ? **No Compilation Errors**
- All methods exist and are called correctly
- Build passes successfully

## How It Works Now

### With Map Data Available:
1. Get all states from `HybridMapManager`
2. Extract unique country names from states
3. Build `IndexedCountryFeature` list from state data
4. Call `Economy.GenerateWorldEconomyFromMapData()` with extracted data
5. Generate cities, states, and economies based on real map geography

### Without Map Data (Fallback):
1. Call `Economy.InitializeWorldEconomy()`
2. Generate procedural world with template-based city generation
3. Create realistic economies using city type templates

### Final Steps (Both Paths):
1. Register economy city anchors for map visualization
2. Set up player as Prime Minister
3. Initialize trade systems
4. Mark economy as initialized

## Testing
- ? Build successful
- ? No compilation errors
- ? Logic verified correct

## Files Modified
- `Views\GameView.axaml.cs` - Fixed `InitializeEconomyData()` method

## Related Systems
This fix enables proper integration with:
- `Economy.cs` - World economy generation
- `ProceduralWorldGenerator.cs` - Template-based city generation
- `HybridMapManager.cs` - Map state data
- `CityTemplates.cs` - City type templates
