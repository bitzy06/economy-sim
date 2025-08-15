using System;
using System.Diagnostics;

namespace Economy_sim
{
    /// <summary>
    /// Simple test to validate and demonstrate state rendering setup
    /// </summary>
    public static class StateRenderingTest
    {
        /// <summary>
        /// Run a comprehensive test of state rendering functionality
        /// </summary>
        public static void RunStateRenderingValidation()
        {
            Console.WriteLine("=== STATE RENDERING VALIDATION TEST ===");
            Console.WriteLine();

            // Test 1: Check data file setup
            Console.WriteLine("1. Checking Natural Earth states data setup...");
            bool setupValid = StateRenderingSetup.ValidateStateRenderingSetup(showConsoleOutput: true);
            Console.WriteLine();

            // Test 2: Initialize managers to check for errors
            Console.WriteLine("2. Testing StatesBorderManager initialization...");
            try
            {
                var statesManager = new StatesBorderManager();
                Console.WriteLine("✅ StatesBorderManager initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ StatesBorderManager initialization failed: {ex.Message}");
            }
            Console.WriteLine();

            // Test 3: Test state mask creation
            Console.WriteLine("3. Testing state mask generation...");
            try
            {
                var statesManager = new StatesBorderManager();
                var testMask = statesManager.CreateStatesMask(100, 100, "US"); // Test with US
                
                // Count non-zero pixels to see if we have any state data
                int nonZeroPixels = 0;
                for (int y = 0; y < testMask.GetLength(0); y++)
                {
                    for (int x = 0; x < testMask.GetLength(1); x++)
                    {
                        if (testMask[y, x] > 0) nonZeroPixels++;
                    }
                }
                
                if (nonZeroPixels > 0)
                {
                    Console.WriteLine($"✅ State mask contains {nonZeroPixels} state pixels (data is working!)");
                }
                else
                {
                    Console.WriteLine("⚠️  State mask contains no data (expected if Natural Earth data is missing)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ State mask generation failed: {ex.Message}");
            }
            Console.WriteLine();

            // Test 4: Test HybridMapManager integration
            Console.WriteLine("4. Testing HybridMapManager state integration...");
            try
            {
                var hybridManager = new HybridMapManager(1024, 512);
                bool canRenderStates = hybridManager.ShouldRenderStates(3); // Zoom level 3
                Console.WriteLine($"✅ HybridMapManager initialized, should render states at zoom 3: {canRenderStates}");
                
                // Test validation through hybrid manager
                bool hybridValidation = hybridManager.ValidateStateRenderingSetup(showOutput: false);
                Console.WriteLine($"✅ HybridMapManager validation result: {hybridValidation}");
                
                hybridManager.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ HybridMapManager test failed: {ex.Message}");
            }
            Console.WriteLine();

            // Summary
            Console.WriteLine("=== SUMMARY ===");
            if (setupValid)
            {
                Console.WriteLine("🎉 State rendering is properly configured and should work!");
                Console.WriteLine("   To see states:");
                Console.WriteLine("   1. Switch to Political View");
                Console.WriteLine("   2. Select a country (left-click)");
                Console.WriteLine("   3. Zoom to level 3 or higher");
                Console.WriteLine("   4. States should appear with gray borders");
                Console.WriteLine("   5. Click a state to select it (yellow border)");
            }
            else
            {
                Console.WriteLine("❌ State rendering setup is incomplete.");
                Console.WriteLine("   Follow the instructions above to download Natural Earth data.");
            }
        }
    }
}