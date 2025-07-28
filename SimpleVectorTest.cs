using System;
using System.Reflection;

namespace StrategyGame.Testing
{
    /// <summary>
    /// Simple test to run vector graphics demonstration
    /// </summary>
    public class SimpleVectorTest
    {
        public static void Main()
        {
            Console.WriteLine("=== VECTOR GRAPHICS CONVERSION TEST ===");
            
            try
            {
                // Test the vector graphics demonstration
                VectorTestRunner.RunTest();
                
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