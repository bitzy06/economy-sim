using Economy_sim.OpenGL;
using OpenTK.Mathematics;
using SkiaSharp;
using System;
using System.Diagnostics;

namespace Economy_sim.Tests
{
    /// <summary>
    /// Simple test to verify OpenGL components can be instantiated and used
    /// </summary>
    public static class OpenGLComponentTest
    {
        public static void RunBasicTests()
        {
            try
            {
                Console.WriteLine("Running OpenGL component tests...");

                // Test 1: Create OpenGL renderer
                Console.WriteLine("Test 1: Creating OpenGL renderer...");
                var renderer = new OpenGLMapRenderer();
                Console.WriteLine("✓ OpenGL renderer created successfully");

                // Test 2: Test basic OpenGL control instantiation
                Console.WriteLine("Test 2: Creating OpenGL control...");
                var control = new OpenGLControl();
                Console.WriteLine("✓ OpenGL control created successfully");

                // Test 3: Test property access
                Console.WriteLine("Test 3: Testing property access...");
                control.ZoomLevel = 1.5f;
                control.ViewOffset = new Vector2(10, 20);
                Console.WriteLine($"✓ ZoomLevel: {control.ZoomLevel}, ViewOffset: {control.ViewOffset}");

                // Test 4: Create a test bitmap
                Console.WriteLine("Test 4: Creating test bitmap...");
                var bitmap = new SKBitmap(256, 256);
                bitmap.Erase(SKColors.Blue);
                Console.WriteLine("✓ Test bitmap created successfully");

                Console.WriteLine("All OpenGL component tests passed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ OpenGL component test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}