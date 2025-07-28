using System;
using System.Reflection;

namespace StrategyGame.Testing
{
    /// <summary>
    /// Simple test to run vector graphics demonstration
    /// </summary>
    public class SimpleVectorTest
    {
        public static void RunTest()
        {
            Console.WriteLine("=== VECTOR GRAPHICS CONVERSION TEST ===");
            
            try
            {
                // Test the vector graphics demonstration
                VectorTestRunner.RunTest();
                
                Console.WriteLine();
                Console.WriteLine("Running vector logic tests...");
                VectorLogicTest.RunLogicTest();
                
                Console.WriteLine();
                Console.WriteLine("All tests completed successfully!");
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}