using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using OSGeo.GDAL;
using OSGeo.OGR;

namespace Economy_sim
{
    /// <summary>
    /// Optimized political mask generator using spatial indexing and GDAL reuse
    /// </summary>
    public class OptimizedPoliticalMaskGenerator : IDisposable
    {
        private readonly PoliticalSpatialIndex _spatialIndex;
        private readonly object _gdalLock = new object();
        
        // Reusable GDAL objects to reduce overhead
        private OSGeo.GDAL.Driver? _memDriver;
        private OSGeo.OGR.Driver? _memDriverOgr;
        
        // Cache for coordinate transformations to avoid repeated calculations
        private readonly ConcurrentDictionary<string, GeoBounds> _coordTransformCache = new();
        
        public OptimizedPoliticalMaskGenerator(PoliticalSpatialIndex spatialIndex)
        {
            _spatialIndex = spatialIndex;
            InitializeGdalDrivers();
        }

        private void InitializeGdalDrivers()
        {
            lock (_gdalLock)
            {
                _memDriver = Gdal.GetDriverByName("MEM");
                _memDriverOgr = Ogr.GetDriverByName("Memory");
            }
        }

        /// <summary>
        /// Generates optimized political mask for a specific tile using spatial indexing
        /// </summary>
        public int[,]? GenerateOptimizedMask(int cellSize, int pixelX, int pixelY, int tileWidth, int tileHeight, int baseWidth, int baseHeight)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // Calculate geographic bounds using cached transformation
                var bounds = GetCachedTileBounds(cellSize, pixelX, pixelY, tileWidth, tileHeight, baseWidth, baseHeight);
                
                Debug.WriteLine($"Generating optimized mask for bounds: {bounds}");

                // Get only countries that intersect with this tile using spatial index
                var relevantCountries = _spatialIndex.GetCountriesInBounds(bounds);
                
                if (relevantCountries.Count == 0)
                {
                    Debug.WriteLine($"No countries found in bounds {bounds}");
                    return CreateEmptyMask(tileWidth, tileHeight);
                }

                Debug.WriteLine($"Found {relevantCountries.Count} relevant countries for tile bounds");

                // Generate mask using only relevant countries
                var mask = RasterizeCountriesOptimized(relevantCountries, bounds, tileWidth, tileHeight);
                
                Debug.WriteLine($"Optimized mask generated in {sw.ElapsedMilliseconds}ms for {relevantCountries.Count} countries");
                return mask;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in optimized mask generation: {ex.Message}");
                return null;
            }
        }

        private GeoBounds GetCachedTileBounds(int cellSize, int pixelX, int pixelY, int tileWidth, int tileHeight, int baseWidth, int baseHeight)
        {
            // Create cache key for coordinate transformation
            string cacheKey = $"{cellSize}_{pixelX}_{pixelY}_{tileWidth}_{tileHeight}_{baseWidth}_{baseHeight}";
            
            if (_coordTransformCache.TryGetValue(cacheKey, out var cachedBounds))
            {
                return cachedBounds;
            }

            // Calculate bounds using the same logic as the main system
            int scaledMapWidth = baseWidth * cellSize;
            int scaledMapHeight = baseHeight * cellSize;
            
            int tileX = pixelX / 512; // TileSizePx is 512
            int tileY = pixelY / 512;
            
            var coordBounds = CoordinateTransform.GetTileGeographicBounds(
                tileX, tileY, 512, scaledMapWidth, scaledMapHeight);

            var bounds = new GeoBounds
            {
                MinLon = coordBounds.MinLon,
                MinLat = coordBounds.MinLat,
                MaxLon = coordBounds.MaxLon,
                MaxLat = coordBounds.MaxLat
            };
            
            // Cache the result if cache isn't too large
            if (_coordTransformCache.Count < 1000)
            {
                _coordTransformCache.TryAdd(cacheKey, bounds);
            }

            return bounds;
        }

        private int[,]? RasterizeCountriesOptimized(System.Collections.Generic.List<IndexedCountryFeature> countries, GeoBounds bounds, int width, int height)
        {
            lock (_gdalLock)
            {
                try
                {
                    if (_memDriver == null || _memDriverOgr == null)
                    {
                        Debug.WriteLine("GDAL drivers not initialized");
                        return null;
                    }

                    // Create in-memory raster dataset
                    Dataset maskDs = _memDriver.Create("", width, height, 1, DataType.GDT_Int32, null);
                    
                    // Set geotransform
                    double[] geoTransform = new double[6];
                    geoTransform[0] = bounds.MinLon; // Top-left X
                    geoTransform[1] = (bounds.MaxLon - bounds.MinLon) / width; // Pixel width
                    geoTransform[2] = 0; // Rotation
                    geoTransform[3] = bounds.MaxLat; // Top-left Y
                    geoTransform[4] = 0; // Rotation
                    geoTransform[5] = -(bounds.MaxLat - bounds.MinLat) / height; // Pixel height (negative)
                    
                    maskDs.SetGeoTransform(geoTransform);
                    maskDs.SetProjection("GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563]],PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433]]");

                    // Create in-memory vector layer for relevant countries only
                    DataSource memDs = _memDriverOgr.CreateDataSource("temp", new string[0]);
                    Layer filteredLayer = memDs.CreateLayer("filtered", null, wkbGeometryType.wkbPolygon, null);
                    
                    // Add raster code field
                    FieldDefn codeField = new FieldDefn("RASTER_CODE", FieldType.OFTInteger);
                    filteredLayer.CreateField(codeField, 1);
                    
                    FeatureDefn filteredDefn = filteredLayer.GetLayerDefn();

                    // Add only relevant countries to the layer
                    foreach (var country in countries)
                    {
                        try
                        {
                            Feature newFeature = new Feature(filteredDefn);
                            newFeature.SetGeometry(country.Geometry);
                            newFeature.SetField("RASTER_CODE", country.RasterCode);
                            filteredLayer.CreateFeature(newFeature);
                            newFeature.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error adding country {country.CountryCode} to layer: {ex.Message}");
                        }
                    }

                    Debug.WriteLine($"Added {filteredLayer.GetFeatureCount(1)} countries to rasterization layer");

                    // Rasterize the filtered layer
                    if (filteredLayer.GetFeatureCount(1) > 0)
                    {
                        Gdal.RasterizeLayer(maskDs, 1, new[] { 1 }, filteredLayer, IntPtr.Zero, IntPtr.Zero,
                            0, null, new[] { "ATTRIBUTE=RASTER_CODE" }, null, "");
                    }

                    // Read the result
                    Band band = maskDs.GetRasterBand(1);
                    int[] flat = new int[width * height];
                    band.ReadRaster(0, 0, width, height, flat, width, height, 0, 0);

                    // Convert to 2D array
                    int[,] result = new int[height, width];
                    for (int r = 0; r < height; r++)
                    {
                        for (int c = 0; c < width; c++)
                        {
                            result[r, c] = flat[r * width + c];
                        }
                    }

                    // Clean up
                    maskDs.Dispose();
                    memDs.Dispose();

                    return result;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in optimized rasterization: {ex.Message}");
                    return null;
                }
            }
        }

        private int[,] CreateEmptyMask(int width, int height)
        {
            return new int[height, width]; // All zeros (water/no data)
        }

        public void Dispose()
        {
            _coordTransformCache.Clear();
            // GDAL drivers are static and managed by GDAL itself
        }
    }
}