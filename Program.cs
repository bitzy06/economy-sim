// In Program.cs
using Avalonia;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;
using System;
using StrategyGame;
using StrategyGame.Testing;


namespace Economy_sim;

class Program
{
    // Initialization code. Don't forget to add reference to AppBuilderExtensions!
    [STAThread]
    public static void Main(string[] args)
    {
        // Check for test mode arguments
        if (args.Length > 0)
        {
            if (args[0] == "test")
            {
                Console.WriteLine("Running in test mode...");
                Console.WriteLine("=== VECTOR GRAPHICS CONVERSION TEST ===");
                VectorGraphicsDemo.RunDemonstration();
                VectorLogicTest.RunLogicTest();
                Console.WriteLine("All tests completed successfully!");
                return;
            }
            else if (args[0] == "render-test")
            {
                Console.WriteLine("Running vector rendering tests...");
                VectorRenderingTest.TestVectorRendering();
                return;
            }
            else if (args[0] == "simple-test")
            {
                Console.WriteLine("Running simple test mode...");
                StrategyGame.Testing.SimpleVectorTest.RunTest();
                return;
            }
            else if (args[0] == "diagnose")
            {
                Console.WriteLine("Running vector rendering diagnostics...");
                VectorRenderingDiagnostics.RunFullDiagnostics();
                return;
            }
            else if (args[0] == "fix-test")
            {
                Console.WriteLine("Testing rendering fixes...");
                RenderingFixTest.TestRenderingFix();
                return;
            }
            else if (args[0] == "data-test")
            {
                Console.WriteLine("Running vector data integration test...");
                VectorDataTest.RunDataIntegrationTest();
                return;
            }
            else if (args[0] == "debug-data")
            {
                Console.WriteLine("Running vector data debug...");
                VectorDebugger.DebugDataIntegration();
                return;
            }
            else if (args[0] == "debug-terrain")
            {
                Console.WriteLine("Running terrain debug test...");
                try
                {
                    // Initialize GDAL for vector graphics system
                    GdalInit.Ensure();
                    Console.WriteLine("GDAL initialized successfully.");
                    
                    QuickVectorTest.TestTerrainClassification();
                    Console.WriteLine("Terrain debug test completed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in terrain debug test: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                return;
            }
        }
        
        try
        {
            // Normal GUI application startup
            GdalInit.Ensure();
            BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to start UI application: {ex.Message}");
            Console.WriteLine("This may be expected in a headless environment.");
            Console.WriteLine("Use 'dotnet run test', 'dotnet run render-test', or 'dotnet run simple-test' for headless testing.");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();




}



static class GdalInit
{
    static bool done;
    static readonly object sync = new();

    public static void Ensure()
    {
        lock (sync)
        {
            if (done) return;

            GdalBase.ConfigureAll();   // loads native libs & sets paths
            Gdal.AllRegister();        // raster drivers
            Ogr.RegisterAll();         // vector drivers (no OgrBase needed)

            done = true;
        }
    }
}