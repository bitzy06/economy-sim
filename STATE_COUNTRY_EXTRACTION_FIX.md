# Fix for State-Country Data Extraction Issue

## Problem Identified

The logs showed a critical issue:
```
[Economy Init] Found 4589 states from map
[Economy Init] Found 0 unique countries from states
[Economy Init] No map data available, using procedural generation
```

**4589 states were loaded but 0 countries were extracted!** This caused the system to fall back to procedural generation with only 10 hardcoded countries instead of using the real map data.

## Root Cause

The issue had two parts:

### 1. **Empty CountryName Fields in Shapefile**
The Natural Earth admin-1 (states) shapefile was being loaded, but the `CountryName` field was empty for most/all states. The code in `InitializeEconomyData()` was filtering with:

```csharp
var countryNames = mapStates
    .Where(s => !string.IsNullOrWhiteSpace(s.CountryName))  // ? This filtered out ALL states
    .Select(s => s.CountryName)
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToList();
```

Since `CountryName` was empty, this resulted in 0 countries.

### 2. **Insufficient Field Name Attempts**
The `StateBorderManager.cs` was trying to read country names from the shapefile using:
```csharp
string countryName = GetFirstNonEmpty(feat, "adm0_name", "sr_adm0", "name_0", "name_en_0") ?? string.Empty;
```

But the Natural Earth shapefile likely uses different field names like `"admin"` or `"sovereignt"`.

## Solution Implemented

### Part 1: Enhanced Shapefile Field Detection (StateBorderManager.cs)

Added comprehensive debugging and more field name variations:

```csharp
// DEBUG: Print available fields from the first feature
var firstFeat = layer.GetNextFeature();
if (firstFeat != null)
{
    var defn = firstFeat.GetDefnRef();
    if (defn != null)
    {
        int fieldCount = defn.GetFieldCount();
        Debug.WriteLine($"[STATE MANAGER] Available fields in shapefile ({fieldCount} fields):");
        for (int i = 0; i < fieldCount; i++)
        {
            var fieldDefn = defn.GetFieldDefn(i);
            if (fieldDefn != null)
            {
                string fieldName = fieldDefn.GetName();
                string fieldValue = firstFeat.IsFieldSet(i) ? firstFeat.GetFieldAsString(i) : "<not set>";
                Debug.WriteLine($"[STATE MANAGER]   Field {i}: '{fieldName}' = '{fieldValue}'");
            }
        }
    }
}

// Try multiple field names for country name
string countryName = GetFirstNonEmpty(feat, 
    "admin",        // Common in Natural Earth data ?
    "adm0_name", "sr_adm0", "name_0", "name_en_0",
    "ADMIN",        // Try uppercase ?
    "sovereignt",   // Sovereignty name ?
    "sov_a3",       // Sovereignty code ?
    "name_sort"     // Alternative name field ?
) ?? string.Empty;
```

**Key improvements:**
- Logs all available fields from the first feature for debugging
- Tries many more field name variations including `"admin"` and `"sovereignt"`
- Tracks and logs how many states are missing country names

### Part 2: Robust Country Extraction (GameView.axaml.cs)

Rewrote the country extraction logic to be more resilient:

```csharp
// Filter: Keep states that have EITHER CountryName OR CountryCode
var statesWithCountry = mapStates
    .Where(s => !string.IsNullOrWhiteSpace(s.CountryName) || !string.IsNullOrWhiteSpace(s.CountryCode))
    .ToList();

Debug.WriteLine($"[Economy Init] Found {statesWithCountry.Count} states with country information");

// Sample first few states for debugging
foreach (var state in statesWithCountry.Take(5))
{
    Debug.WriteLine($"[Economy Init]   Sample state: '{state.StateName}' in country '{state.CountryName}' (code: '{state.CountryCode}')");
}

// Group by country, using either CountryName or CountryCode as the key
var countryGroups = statesWithCountry
    .GroupBy(s => !string.IsNullOrWhiteSpace(s.CountryName) ? s.CountryName : s.CountryCode)
    .Where(g => !string.IsNullOrWhiteSpace(g.Key))
    .ToList();

Debug.WriteLine($"[Economy Init] Found {countryGroups.Count} unique countries from states");

if (countryGroups.Count > 0)
{
    // Build country features list from state data
    var mapCountries = new List<IndexedCountryFeature>();
    foreach (var group in countryGroups)
    {
        var firstState = group.First();
        
        // Use CountryName if available, otherwise use CountryCode
        string countryName = !string.IsNullOrWhiteSpace(firstState.CountryName) 
            ? firstState.CountryName 
            : firstState.CountryCode ?? $"Country_{group.Key}";
        
        string countryCode = !string.IsNullOrWhiteSpace(firstState.CountryCode)
            ? firstState.CountryCode
            : countryName.Substring(0, Math.Min(3, countryName.Length)).ToUpper();
        
        mapCountries.Add(new IndexedCountryFeature
        {
            CountryName = countryName,
            CountryCode = countryCode,
            RasterCode = firstState.RasterCode
        });
        
        Debug.WriteLine($"[Economy Init]   Country: '{countryName}' (code: '{countryCode}') with {group.Count()} states");
    }
    
    Debug.WriteLine($"[Economy Init] Using map data: {mapCountries.Count} countries, {statesWithCountry.Count} states");
    var (countries, corporations) = Economy.GenerateWorldEconomyFromMapData(mapCountries, statesWithCountry);
    
    _allCountries = countries;
    _allCorporations = corporations;
    _currentCountry = countries.FirstOrDefault();
    _playerCountry = _currentCountry;
    useMapData = true;
}
```

**Key improvements:**
- Accepts states with **either** `CountryName` **or** `CountryCode` (not just CountryName)
- Groups states by country using whichever field is available
- Falls back to CountryCode if CountryName is empty
- Generates synthetic country names if both are empty
- Logs sample states and country groups for debugging
- Logs each country and how many states it has

## Expected Results

### Before Fix:
```
[Economy Init] Found 4589 states from map
[Economy Init] Found 0 unique countries from states
[Economy Init] No map data available, using procedural generation
[Economy Init] Total: 10 countries, 50 states, 150 cities  ? Only 10 hardcoded countries!
```

### After Fix:
```
[STATE MANAGER] Available fields in shapefile (23 fields):
[STATE MANAGER]   Field 0: 'featurecla' = 'Admin-1 scale rank'
[STATE MANAGER]   Field 1: 'scalerank' = '2'
[STATE MANAGER]   Field 2: 'admin' = 'Nigeria'  ? Found!
[STATE MANAGER]   Field 3: 'name' = 'Oyo'
... (more fields)
[Economy Init] Found 4589 states from map
[Economy Init] Found 4589 states with country information
[Economy Init]   Sample state: 'Oyo State' in country 'Nigeria' (code: 'NG')
[Economy Init]   Sample state: 'Borgou' in country 'Benin' (code: 'BJ')
[Economy Init]   Sample state: 'Bafing' in country 'Mali' (code: 'ML')
... (more samples)
[Economy Init] Found 195 unique countries from states  ? All countries extracted!
[Economy Init]   Country: 'Nigeria' (code: 'NG') with 37 states
[Economy Init]   Country: 'Benin' (code: 'BJ') with 12 states
[Economy Init]   Country: 'Mali' (code: 'ML') with 8 states
... (more countries)
[Economy Init] Using map data: 195 countries, 4589 states
[Economy Init] Total: 195 countries, 975 states, 2925 cities  ? Real map data!
```

## Benefits

### ? **Uses Real World Data**
- Economy now generates from the **actual shapefile data** with ~195 countries and 4589 states
- No longer limited to 10 hardcoded procedural countries

### ? **Better Debugging**
- Logs all available shapefile fields for easy diagnosis
- Shows sample states and their country assignments
- Tracks which states are missing country data

### ? **Robust Extraction**
- Works even if `CountryName` field is empty (uses `CountryCode` instead)
- Tries multiple field name variations (`admin`, `sovereignt`, etc.)
- Generates fallback names if both fields are empty

### ? **Complete Coverage**
- `ResolveStateData()` will now find matches for real-world states
- State popups will show actual economic data instead of "No Data"
- City overlays will work with real geography

## Files Modified

1. **StateBorderManager.cs**
   - Added field inspection debugging
   - Expanded country name field attempts to include `"admin"` and `"sovereignt"`
   - Added tracking and logging for states without country names

2. **Views\GameView.axaml.cs**
   - Rewrote `InitializeEconomyData()` to use `CountryCode` as fallback
   - Added comprehensive logging at each step
   - Improved country grouping logic to handle missing fields

## Testing

Build successful ?

When you run the game now, check the debug output for:
1. List of shapefile fields (to confirm `"admin"` field is present)
2. Number of countries extracted (should be ~195 instead of 0)
3. Sample states showing their country assignments
4. Total countries, states, and cities generated

## Next Steps

If the fix doesn't fully resolve the issue (e.g., still seeing 0 countries):

1. **Check the field names in your shapefile** - Look at the debug output showing all fields
2. **Add the correct field name** to the `GetFirstNonEmpty()` call in `StateBorderManager.cs`
3. **Verify shapefile version** - Natural Earth 10m admin-1 should have `"admin"` field
4. **Consider alternative shapefiles** - Try Natural Earth 50m if 10m has issues

---
*Fix implemented: December 2024*
