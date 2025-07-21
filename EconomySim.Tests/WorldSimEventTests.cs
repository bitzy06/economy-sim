using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StrategyGame;
using System.IO;

namespace EconomySim.Tests;

[TestClass]
public class WorldSimEventTests
{
    [TestMethod]
    public async Task DistrictDestroyedEventUpdatesPopulation()
    {
        var city = new City("Testopolis") { Population = 1000, Budget = 1000 };
        WorldSim.RegisterCity(city);
        WorldSim.Initialize();

        MessageBus.Instance.Publish(new DistrictDestroyedEventData(city.Name, "Old Town", 200));
        await Task.Delay(50);
        Assert.AreEqual(800, city.Population);
    }

    [TestMethod]
    public async Task MajorInfrastructureBuiltEventUpdatesBudget()
    {
        var city = new City("BuildCity") { Population = 1000, Budget = 500 };
        WorldSim.RegisterCity(city);
        WorldSim.Initialize();

        MessageBus.Instance.Publish(new MajorInfrastructureBuiltEventData(city.Name, "Railway", 2.0));
        await Task.Delay(50);
        Assert.AreEqual(700, city.Budget, 0.001);
    }

    [TestMethod]
    public async Task EventLoggerRecordsAndReplays()
    {
        var logCity = new City("LogCity") { Population = 1000, Budget = 1000 };
        WorldSim.RegisterCity(logCity);
        WorldSim.Initialize();

        MessageBus.Instance.Publish(new DistrictDestroyedEventData(logCity.Name, "Old Town", 100));
        await Task.Delay(50);

        var logFile = EventLogger.LogFilePath;
        Assert.IsTrue(File.Exists(logFile));

        var replayCity = new City("ReplayCity") { Population = 1000, Budget = 1000 };
        WorldSim.RegisterCity(replayCity);

        EventReplayer.Replay(logFile);
        await Task.Delay(50);

        Assert.AreEqual(900, replayCity.Population);
    }
}
