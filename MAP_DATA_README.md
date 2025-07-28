# Map Data Files

The economy simulation uses real-world geographic data for map rendering. When these files are missing, the system will automatically generate colorful placeholder tiles so the game can still run.

## Required Data Files

To enable real terrain and country boundary rendering, place the following files in `~/Documents/data/`:

### Terrain Data
- **NE1_HR_LC.tif** - Natural Earth high-resolution land cover raster
  - Download from: https://www.naturalearthdata.com/downloads/10m-raster-data/10m-natural-earth-1/
  - File: Natural Earth I with Shaded Relief, Water, and Drainages

### Country Boundaries
- **ne_10m_admin_0_countries.shp** (and associated files .dbf, .prj, .shx)
  - Download from: https://www.naturalearthdata.com/downloads/10m-cultural-vectors/10m-admin-0-countries/
  - File: Admin 0 – Countries

### Optional Files
- **ETOPO1_Bed_g_geotiff.tif** - Elevation data (for advanced terrain features)
- **ne_10m_urban_areas.shp** - Urban area boundaries
- **ne_10m_populated_places.shp** - Cities and towns

## Fallback Rendering

When data files are missing, the game will:
- Display colorful checkered pattern tiles
- Show console messages indicating missing files
- Provide instructions on where to place the required files
- Continue running normally with placeholder graphics

This ensures the game is playable even without the large geographic data files.