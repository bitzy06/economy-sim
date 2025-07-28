using System;

namespace StrategyGame
{
    /// <summary>
    /// Test program to run the vector graphics demonstration
    /// </summary>
    public static class VectorTestRunner
    {
        public static void RunTest()
        {
            try
            {
                Console.WriteLine("Running Vector Graphics Demonstration...");
                Console.WriteLine();
                
                VectorGraphicsDemo.RunDemonstration();
                VectorGraphicsDemo.SimulatePerformanceComparison();
                
                Console.WriteLine("✅ Vector graphics demonstration completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error during test: {ex.Message}");
            }
        }
    }
}