using System;
using System.IO;
using System.Collections.Generic;

namespace StrategyGame
{
    public static class DebugLogger
    {
        private static string logFilePath;
        private static bool isLoggingEnabled = false; // Logging off by default
        private static bool isDetailedLoggingEnabled = false; // Toggle for detailed logging

        static DebugLogger()
        {
            string logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "logs");
            if (!Directory.Exists(logsDirectory))
            {
                Directory.CreateDirectory(logsDirectory);
            }

            logFilePath = Path.Combine(logsDirectory, $"debug_log_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
        }

        public static bool ToggleLogging()
        {
            isLoggingEnabled = !isLoggingEnabled;
            return isLoggingEnabled;
        }

        public static void EnableDetailedLogging(bool enable)
        {
            isDetailedLoggingEnabled = enable;
        }

        public static void Log(string message)
        {
            if (!isLoggingEnabled) return;

            try
            {
                using (StreamWriter writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine($"[{DateTime.Now}] {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to debug log: {ex.Message}");
            }
        }

        public static void LogDetailed(string message)
        {
            if (!isLoggingEnabled) return;
            if (!isDetailedLoggingEnabled) return;
            Log(message);
        }

        public static void LogCityData(City city)
        {
            if (city == null) return;

            Log($"City: {city.Name}");
            Log($"Population: {city.Population}");
            Log($"Budget: {city.Budget}");
            Log($"QoL: {city.CalculateCityQualityOfLife():0.00}");

            foreach (var suburb in city.Suburbs)
            {
                Log($"  Suburb: {suburb.Name}, QoL: {suburb.CalculateQualityOfLife():0.00}, Housing Capacity: {suburb.HousingCapacity:0.00}, Railway: {suburb.RailwayKilometers:0.00} km");
            }

            foreach (var pop in city.PopClasses)
            {
                Log($"  PopClass: {pop.Name}, Size: {pop.Size}, Employed: {pop.Employed}, QoL: {pop.QualityOfLife:0.00}, Happiness: {pop.Happiness}");
            }
        }

        public static void LogDetailedCityData(City city)
        {
            if (city == null) return;

            Log($"--- Detailed Data for City: {city.Name} ---");
            Log($"Population: {city.Population}");
            Log($"Budget: {city.Budget}");
            Log($"QoL: {city.CalculateCityQualityOfLife():0.00}");
            Log($"City Expenses: {city.CityExpenses}");

            foreach (var suburb in city.Suburbs)
            {
                Log($"  Suburb: {suburb.Name}");
                Log($"    Population: {suburb.Population}");
                Log($"    Housing Capacity: {suburb.HousingCapacity:0.00}");
                Log($"    Railway Kilometers: {suburb.RailwayKilometers:0.00}");
                Log($"    QoL: {suburb.CalculateQualityOfLife():0.00}");
            }

            foreach (var pop in city.PopClasses)
            {
                Log($"  PopClass: {pop.Name}");
                Log($"    Size: {pop.Size}");
                Log($"    Employed: {pop.Employed}");
                Log($"    Unemployed: {pop.Unemployed}");
                Log($"    Income Per Person: {pop.IncomePerPerson:0.00}");
                Log($"    QoL: {pop.QualityOfLife:0.00}");
                Log($"    Happiness: {pop.Happiness}");
                Log($"    Needs:");
                foreach (var need in pop.Needs)
                {
                    Log($"      {need.Key}: {need.Value}");
                }
                Log($"    Unmet Needs: {pop.UnmetNeeds}");
            }

            foreach (var good in city.LocalSupply.Keys)
            {
                double price = city.LocalPrices.ContainsKey(good) ? city.LocalPrices[good] : 0;
                Log($"  Good: {good}");
                Log($"    Supply: {city.LocalSupply[good]}");
                Log($"    Demand: {city.LocalDemand[good]}");
                Log($"    Price: {price:0.00}");
            }

            Log($"--- End of Detailed Data for City: {city.Name} ---");
        }

        public static void LogCountryData(Country country)
        {
            if (country == null) return;

            Log($"Country: {country.Name}");
            Log($"Budget: {country.Budget}");

            foreach (var state in country.States)
            {
                Log($"  State: {state.Name}, Budget: {state.Budget}");

                foreach (var city in state.Cities)
                {
                    LogCityData(city);
                }
            }
        }

        public static void FinalizeLog(List<Country> allCountries)
        {
            Log("--- Final Data Dump ---");

            // Log summary of all countries
            foreach (var country in allCountries)
            {
                LogCountryData(country);
            }

            Log("--- Program Closed ---");
        }
    }
}
