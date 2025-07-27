// In Program.cs
using Avalonia;
using MaxRev.Gdal.Core;
using OSGeo.GDAL;
using OSGeo.OGR;
using System;


namespace Economy_sim;

class Program
{
    // Initialization code. Don't forget to add reference to AppBuilderExtensions!
    [STAThread]
    public static void Main(string[] args)
    {
        // Check if this is a test run
        if (args.Length > 0 && args[0] == "--test-tiles")
        {
            RunTileTest();
            return;
        }

        GdalInit.Ensure();
        BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void RunTileTest()
    {
        try 
        {
            GdalInit.Ensure();
            
            Console.WriteLine("Testing tile generation...");
            
            // Test parameters that match the debug output from the problem statement
            int mapWidth = 256;
            int mapHeight = 256;
            int cellSize = 3;
            int tileX = 0;
            int tileY = 0;
            int tileSizePx = 512;
            
            Console.WriteLine($"Generating tile with mapWidth={mapWidth}, mapHeight={mapHeight}, cellSize={cellSize}, tileX={tileX}, tileY={tileY}, tileSizePx={tileSizePx}");
            
            var tile = StrategyGame.PixelMapGenerator.GenerateTileWithCountriesLarge(mapWidth, mapHeight, cellSize, tileX, tileY, tileSizePx);
            
            Console.WriteLine($"Generated tile: {tile.Width}x{tile.Height}");
            Console.WriteLine($"Tile has content: {tile != null}");
            
            // Test a few more tiles
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    var testTile = StrategyGame.PixelMapGenerator.GenerateTileWithCountriesLarge(mapWidth, mapHeight, cellSize, x, y, tileSizePx);
                    Console.WriteLine($"Tile ({x},{y}): {testTile.Width}x{testTile.Height}");
                    testTile.Dispose();
                }
            }
            
            // Test the MultiResolutionMapManager
            Console.WriteLine("\nTesting MultiResolutionMapManager...");
            var mapManager = new StrategyGame.MultiResolutionMapManager(baseWidth: 256, baseHeight: 256);
            
            // Test parameters matching the debug output from problem statement
            float zoom = 1.0f;
            var viewArea = new SkiaSharp.SKRectI(0, 0, 2540, 1400);
            
            Console.WriteLine($"AssembleView: Zoom={zoom}, ViewArea={{Left={viewArea.Left},Top={viewArea.Top},Width={viewArea.Width},Height={viewArea.Height}}}");
            Console.WriteLine($"CellSize for zoom {zoom} = {mapManager.GetCellSize(zoom)}");
            
            using var bitmap = mapManager.AssembleView(zoom, viewArea, null);
            Console.WriteLine($"Got bitmap {bitmap.Width}x{bitmap.Height}");
            Console.WriteLine($"Bitmap has content: {!bitmap.IsEmpty}");
            
            tile.Dispose();
            Console.WriteLine("Test completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Test failed: {ex}");
        }
    }




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