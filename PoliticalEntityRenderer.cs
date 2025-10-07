using System;
using System.Collections.Generic;
using System.Linq;
using SkiaSharp;
using System.Threading.Tasks;
using System.Threading;

namespace Economy_sim
{
    /// <summary>
    /// Unified renderer for political entities (Countries and States) with consistent visual styling
    /// and efficient rendering capabilities. Combines the rendering logic that was previously scattered
    /// across separate Country and State rendering implementations.
    /// </summary>
    public class PoliticalEntityRenderer
    {
        private readonly PoliticalDataCache _dataCache;
        
        // Rendering settings
        private const uint DefaultBorderColor = 0xFF000000; // Black
        private const uint SelectedBorderColor = 0xFFFFFFFF; // White
        private const uint WaterColor = 0xFF87CEEB; // LightSkyBlue
        private const int DefaultBorderWidth = 1;
        private const int SelectedBorderWidth = 2;

        // Thread-safe random for visual variation
        private static readonly ThreadLocal<Random> ThreadLocalRandom = new ThreadLocal<Random>(
            () => new Random(Environment.TickCount + Thread.CurrentThread.ManagedThreadId));

        public PoliticalEntityRenderer(PoliticalDataCache dataCache)
        {
            _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        }

        /// <summary>
        /// Renders a country entity with its associated visual properties
        /// </summary>
        public SKBitmap? RenderCountry(Country country, int width, int height, bool isSelected = false)
        {
            if (country == null || width <= 0 || height <= 0)
                return null;

            try
            {
                var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
                bitmap.Erase(WaterColor);

                using var canvas = new SKCanvas(bitmap);
                using var paint = new SKPaint();

                // Get country color from data cache or generate one based on name
                var countryColor = GetCountryColor(country);
                
                // Fill country area
                paint.Color = countryColor;
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawRect(0, 0, width, height, paint);

                // Draw country border
                paint.Color = isSelected ? new SKColor(SelectedBorderColor) : new SKColor(DefaultBorderColor);
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = isSelected ? SelectedBorderWidth : DefaultBorderWidth;
                canvas.DrawRect(0, 0, width, height, paint);

                // Render states within the country if they exist
                if (country.States != null && country.States.Any())
                {
                    RenderStatesWithinCountry(canvas, country.States, width, height, paint);
                }

                // Add country label if space permits
                if (width > 100 && height > 50)
                {
                    RenderCountryLabel(canvas, country.Name, width, height);
                }

                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Renders a state entity with its associated visual properties
        /// </summary>
        public SKBitmap? RenderState(State state, int width, int height, bool isSelected = false)
        {
            if (state == null || width <= 0 || height <= 0)
                return null;

            try
            {
                var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
                bitmap.Erase(WaterColor);

                using var canvas = new SKCanvas(bitmap);
                using var paint = new SKPaint();

                // Get state color (lighter variation of country color or unique state color)
                var stateColor = GetStateColor(state);
                
                // Fill state area
                paint.Color = stateColor;
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawRect(0, 0, width, height, paint);

                // Draw state border
                paint.Color = isSelected ? new SKColor(SelectedBorderColor) : new SKColor(DefaultBorderColor);
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = isSelected ? SelectedBorderWidth : DefaultBorderWidth;
                canvas.DrawRect(0, 0, width, height, paint);

                // Render cities within the state if they exist
                if (state.Cities != null && state.Cities.Any())
                {
                    RenderCitiesWithinState(canvas, state.Cities, width, height, paint);
                }

                // Add state label if space permits
                if (width > 80 && height > 40)
                {
                    RenderStateLabel(canvas, state.Name, width, height);
                }

                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Renders both countries and states in a unified view
        /// </summary>
        public SKBitmap? RenderPoliticalEntities(IEnumerable<Country> countries, int width, int height, 
            Country? selectedCountry = null, State? selectedState = null)
        {
            if (countries == null || width <= 0 || height <= 0)
                return null;

            try
            {
                var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
                bitmap.Erase(WaterColor);

                using var canvas = new SKCanvas(bitmap);
                using var paint = new SKPaint();

                // Calculate layout for countries (simple grid layout for demo)
                var countryList = countries.ToList();
                if (!countryList.Any()) return bitmap;

                int cols = (int)Math.Ceiling(Math.Sqrt(countryList.Count));
                int rows = (int)Math.Ceiling((double)countryList.Count / cols);
                
                int countryWidth = width / cols;
                int countryHeight = height / rows;

                for (int i = 0; i < countryList.Count; i++)
                {
                    var country = countryList[i];
                    int x = (i % cols) * countryWidth;
                    int y = (i / cols) * countryHeight;
                    
                    bool isCountrySelected = selectedCountry != null && country.Name == selectedCountry.Name;
                    
                    // Render country area
                    var countryRect = new SKRect(x, y, x + countryWidth, y + countryHeight);
                    var countryColor = GetCountryColor(country);
                    
                    paint.Color = countryColor;
                    paint.Style = SKPaintStyle.Fill;
                    canvas.DrawRect(countryRect, paint);

                    // Render states within country
                    if (country.States != null && country.States.Any())
                    {
                        RenderStatesInRegion(canvas, country.States, countryRect, selectedState, paint);
                    }

                    // Draw country border
                    paint.Color = isCountrySelected ? new SKColor(SelectedBorderColor) : new SKColor(DefaultBorderColor);
                    paint.Style = SKPaintStyle.Stroke;
                    paint.StrokeWidth = isCountrySelected ? SelectedBorderWidth : DefaultBorderWidth;
                    canvas.DrawRect(countryRect, paint);
                }

                return bitmap;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the appropriate color for a country, using data cache or generating one
        /// </summary>
        private SKColor GetCountryColor(Country country)
        {
            try
            {
                // Try to get color from political data cache first
                if (_dataCache != null)
                {
                    // Note: This assumes there's a way to map country name to raster code
                    // In a real implementation, you might need a lookup table
                    var color = _dataCache.GetCountryColorByName(country.Name);
                    if (color != SKColors.Transparent)
                        return color;
                }

                // Fallback: Generate color based on country name hash
                return GenerateColorFromName(country.Name);
            }
            catch
            {
                return GenerateColorFromName(country.Name);
            }
        }

        /// <summary>
        /// Gets the appropriate color for a state (lighter variation of parent country or unique)
        /// </summary>
        private SKColor GetStateColor(State state)
        {
            // Generate a consistent color based on state name
            var baseColor = GenerateColorFromName(state.Name);
            
            // Make it slightly lighter for better distinction from country colors
            var rng = ThreadLocalRandom.Value;
            
            // Convert to HSL manually and adjust
            baseColor.ToHsl(out float h, out float s, out float l);
            
            // Reduce saturation slightly and increase lightness
            float newSaturation = Math.Max(0.3f, s - 0.2f);
            float newLightness = Math.Min(0.9f, l + 0.2f);
            
            return SKColor.FromHsl(h, newSaturation, newLightness);
        }

        /// <summary>
        /// Generates a consistent color based on a name hash
        /// </summary>
        private SKColor GenerateColorFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return SKColors.Gray;

            var hash = name.GetHashCode();
            var rng = new Random(hash); // Use hash as seed for consistency
            
            // Generate pleasant, distinguishable colors
            float hue = rng.Next(360);
            float saturation = 0.6f + (rng.Next(40) / 100.0f); // 0.6-1.0
            float lightness = 0.4f + (rng.Next(40) / 100.0f);   // 0.4-0.8
            
            return SKColor.FromHsl(hue, saturation, lightness);
        }

        /// <summary>
        /// Renders states within a country area
        /// </summary>
        private void RenderStatesWithinCountry(SKCanvas canvas, IList<State> states, int width, int height, SKPaint paint)
        {
            if (!states.Any()) return;

            // Simple grid layout for states within country
            int stateCols = (int)Math.Ceiling(Math.Sqrt(states.Count));
            int stateRows = (int)Math.Ceiling((double)states.Count / stateCols);
            
            int stateWidth = width / stateCols;
            int stateHeight = height / stateRows;

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                int x = (i % stateCols) * stateWidth;
                int y = (i / stateCols) * stateHeight;
                
                var stateRect = new SKRect(x, y, x + stateWidth, y + stateHeight);
                var stateColor = GetStateColor(state);
                
                // Fill state area with a slightly different color
                paint.Color = stateColor;
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawRect(stateRect, paint);
                
                // Draw state border (thinner than country border)
                paint.Color = new SKColor(DefaultBorderColor);
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = 0.5f;
                canvas.DrawRect(stateRect, paint);
            }
        }

        /// <summary>
        /// Renders states within a specific region/country rectangle
        /// </summary>
        private void RenderStatesInRegion(SKCanvas canvas, IList<State> states, SKRect region, State? selectedState, SKPaint paint)
        {
            if (!states.Any()) return;

            int stateCols = (int)Math.Ceiling(Math.Sqrt(states.Count));
            int stateRows = (int)Math.Ceiling((double)states.Count / stateCols);
            
            float stateWidth = region.Width / stateCols;
            float stateHeight = region.Height / stateRows;

            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                float x = region.Left + (i % stateCols) * stateWidth;
                float y = region.Top + (i / stateCols) * stateHeight;
                
                var stateRect = new SKRect(x, y, x + stateWidth, y + stateHeight);
                var stateColor = GetStateColor(state);
                
                bool isStateSelected = selectedState != null && state.Name == selectedState.Name;
                
                // Fill state area
                paint.Color = stateColor;
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawRect(stateRect, paint);
                
                // Draw state border
                paint.Color = isStateSelected ? new SKColor(SelectedBorderColor) : new SKColor(DefaultBorderColor);
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = isStateSelected ? SelectedBorderWidth : 0.5f;
                canvas.DrawRect(stateRect, paint);
            }
        }

        /// <summary>
        /// Renders cities within a state area as small dots
        /// </summary>
        private void RenderCitiesWithinState(SKCanvas canvas, IList<City> cities, int width, int height, SKPaint paint)
        {
            if (!cities.Any()) return;

            paint.Color = SKColors.DarkRed;
            paint.Style = SKPaintStyle.Fill;
            
            var rng = ThreadLocalRandom.Value;
            
            foreach (var city in cities)
            {
                // Place cities at random but consistent positions within the state
                var cityHash = city.Name?.GetHashCode() ?? 0;
                var cityRng = new Random(cityHash);
                
                float x = cityRng.Next(width * 20 / 100, width * 80 / 100); // 20%-80% of width
                float y = cityRng.Next(height * 20 / 100, height * 80 / 100); // 20%-80% of height
                
                // Draw city as a small circle
                float radius = Math.Min(width, height) * 0.05f; // 5% of smaller dimension
                canvas.DrawCircle(x, y, radius, paint);
            }
        }

        /// <summary>
        /// Renders a country label if there's enough space
        /// </summary>
        private void RenderCountryLabel(SKCanvas canvas, string countryName, int width, int height)
        {
            if (string.IsNullOrEmpty(countryName)) return;

            using var textPaint = new SKPaint();
            textPaint.Color = SKColors.Black;
            textPaint.TextSize = Math.Min(width, height) * 0.1f; // Scale text to region size
            textPaint.IsAntialias = true;
            
            var textBounds = new SKRect();
            textPaint.MeasureText(countryName, ref textBounds);
            
            // Center the text
            float x = (width - textBounds.Width) / 2;
            float y = (height + textBounds.Height) / 2;
            
            // Draw text with white background for better visibility
            using var bgPaint = new SKPaint();
            bgPaint.Color = SKColors.White.WithAlpha(180);
            var bgRect = new SKRect(x - 5, y - textBounds.Height - 2, x + textBounds.Width + 5, y + 2);
            canvas.DrawRect(bgRect, bgPaint);
            
            canvas.DrawText(countryName, x, y, textPaint);
        }

        /// <summary>
        /// Renders a state label if there's enough space
        /// </summary>
        private void RenderStateLabel(SKCanvas canvas, string stateName, int width, int height)
        {
            if (string.IsNullOrEmpty(stateName)) return;

            using var textPaint = new SKPaint();
            textPaint.Color = SKColors.DarkBlue;
            textPaint.TextSize = Math.Min(width, height) * 0.08f; // Slightly smaller than country labels
            textPaint.IsAntialias = true;
            
            var textBounds = new SKRect();
            textPaint.MeasureText(stateName, ref textBounds);
            
            // Center the text
            float x = (width - textBounds.Width) / 2;
            float y = (height + textBounds.Height) / 2;
            
            // Draw text with semi-transparent white background
            using var bgPaint = new SKPaint();
            bgPaint.Color = SKColors.White.WithAlpha(120);
            var bgRect = new SKRect(x - 3, y - textBounds.Height - 1, x + textBounds.Width + 3, y + 1);
            canvas.DrawRect(bgRect, bgPaint);
            
            canvas.DrawText(stateName, x, y, textPaint);
        }
    }

    /// <summary>
    /// Extension methods for political data cache to support country name lookups
    /// </summary>
    public static class PoliticalDataCacheExtensions
    {
        /// <summary>
        /// Gets country color by name (fallback method if not in original cache)
        /// </summary>
        public static SKColor GetCountryColorByName(this PoliticalDataCache cache, string countryName)
        {
            // This is a placeholder - in a real implementation, you would have
            // a mapping from country names to raster codes or a separate lookup
            return SKColors.Transparent; // Indicate not found
        }
    }
}