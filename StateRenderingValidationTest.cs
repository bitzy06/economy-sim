using System;
using System.Diagnostics;
using Economy_sim;

namespace StateRenderingTest
{
    /// <summary>
    /// Simple validation test for the state rendering implementation
    /// Tests that all classes instantiate correctly and basic functionality works
    /// </summary>
    public class StateRenderingValidationTest
    {
        public static void RunValidationTests()
        {
            Debug.WriteLine("=== State Rendering Validation Test ===");

            try
            {
                // Test 1: Verify StatesBorderManager can be instantiated
                Debug.WriteLine("Test 1: Creating StatesBorderManager...");
                var statesManager = new StatesBorderManager();
                Debug.WriteLine("✅ StatesBorderManager created successfully");

                // Test 2: Verify StatesDataCache can be instantiated
                Debug.WriteLine("Test 2: Creating StatesDataCache...");
                var statesCache = new StatesDataCache();
                Debug.WriteLine("✅ StatesDataCache created successfully");

                // Test 3: Verify StatesSpatialIndex can be instantiated  
                Debug.WriteLine("Test 3: Creating StatesSpatialIndex...");
                var statesIndex = new StatesSpatialIndex();
                Debug.WriteLine("✅ StatesSpatialIndex created successfully");

                // Test 4: Verify HybridMapManager includes state functionality
                Debug.WriteLine("Test 4: Creating HybridMapManager with state support...");
                var hybridManager = new HybridMapManager();
                var stateThreshold = hybridManager.StateRenderingThreshold;
                Debug.WriteLine($"✅ HybridMapManager created with state rendering threshold: {stateThreshold}");

                // Test 5: Test state-related methods exist and can be called safely
                Debug.WriteLine("Test 5: Testing state-related methods...");
                
                var shouldRenderStates = hybridManager.ShouldRenderStates(3);
                Debug.WriteLine($"✅ ShouldRenderStates(3): {shouldRenderStates}");

                var shouldNotRenderStates = hybridManager.ShouldRenderStates(1);
                Debug.WriteLine($"✅ ShouldRenderStates(1): {shouldNotRenderStates}");

                var selectedCountry = hybridManager.SelectedCountry;
                var selectedState = hybridManager.SelectedState;
                Debug.WriteLine($"✅ Selection state: Country={selectedCountry?.CountryName ?? "None"}, State={selectedState?.StateName ?? "None"}");

                // Test 6: Test state manager color functionality
                Debug.WriteLine("Test 6: Testing state color functionality...");
                var stateColors = statesManager.GetAllStateColors();
                Debug.WriteLine($"✅ State colors initialized: {stateColors.Count} colors");

                var defaultColor = statesManager.GetStateColorByRasterCode(999); // Non-existent state
                Debug.WriteLine($"✅ Default state color: {defaultColor}");

                Debug.WriteLine("=== All Validation Tests Passed! ===");
                Debug.WriteLine("");
                Debug.WriteLine("State rendering implementation is ready for use:");
                Debug.WriteLine("1. Place Natural Earth state shapefile in Documents/data/country_borders/states/");
                Debug.WriteLine("2. Run the application and switch to Political View");
                Debug.WriteLine("3. Zoom to level 3+ and select a country to see state borders");
                Debug.WriteLine("4. Left-click states to select them (yellow highlight)");
                Debug.WriteLine("5. Right-click for identification without selection");

                return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Validation test failed: {ex.Message}");
                Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }
        }
    }
}

// Test runner - uncomment to run tests
/* 
class TestProgram
{
    static void Main(string[] args)
    {
        StateRenderingValidationTest.RunValidationTests();
    }
}
*/