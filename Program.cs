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
        GdalInit.Ensure();
        
        // Run tests if --test argument is provided
        if (args.Length > 0 && args[0] == "--test")
        {
            Console.WriteLine("Running Authoritative Grid Tests...");
            _ = AuthoritativeGridTest.RunTests();
            Console.WriteLine("Press any key to continue to the main application...");
            Console.ReadKey();
        }
        
        BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);
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