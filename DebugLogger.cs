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

        // Category toggles
        public enum LogCategory
        {
            General,
            Pop,
            Building,
            Economy
        }

        private static bool logPopStats = false;
        private static bool logBuildingStats = false;
        private static bool logEconomyStats = false;

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

        public static void EnablePopLogging(bool enable) => logPopStats = enable;
        public static void EnableBuildingLogging(bool enable) => logBuildingStats = enable;
        public static void EnableEconomyLogging(bool enable) => logEconomyStats = enable;

        public static void EnableDetailedLogging(bool enable)
        {
            isDetailedLoggingEnabled = enable;
        }

        public static void Log(string message, LogCategory category = LogCategory.General)
        {
            if (!isLoggingEnabled) return;

            switch (category)
            {
                case LogCategory.Pop:
                    if (!logPopStats) return;
                    break;
                case LogCategory.Building:
                    if (!logBuildingStats) return;
                    break;
                case LogCategory.Economy:
                    if (!logEconomyStats) return;
                    break;
            }

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

        public static void LogDetailed(string message, LogCategory category = LogCategory.General)
        {
            if (!isLoggingEnabled) return;
            if (!isDetailedLoggingEnabled) return;
            Log(message, category);
        }

        public static void LogCityData(City city)
        {
            if (city == null) return;

            Log($"City: {city.Name}", LogCategory.Economy);
            Log($"Population: {city.Population}", LogCategory.Pop);
            Log($"Budget: {city.Budget}", LogCategory.Economy);
            Log($"QoL: {city.CalculateCityQualityOfLife():0.00}", LogCategory.Economy);

            foreach (var suburb in city.Suburbs)
            {
                Log($"  Suburb: {suburb.Name}, QoL: {suburb.CalculateQualityOfLife():0.00}, Housing Capacity: {suburb.HousingCapacity:0.00}, Railway: {suburb.RailwayKilometers:0.00} km", LogCategory.Building);
            }

            foreach (var pop in city.PopClasses)
            {
                Log($"  PopClass: {pop.Name}, Size: {pop.Size}, Employed: {pop.Employed}, QoL: {pop.QualityOfLife:0.00}, Happiness: {pop.Happiness}", LogCategory.Pop);
            }
        }

        public static void LogDetailedCityData(City city)
        {
            if (city == null) return;

            Log($"--- Detailed Data for City: {city.Name} ---", LogCategory.Economy);
            Log($"Population: {city.Population}", LogCategory.Pop);
            Log($"Budget: {city.Budget}", LogCategory.Economy);
            Log($"QoL: {city.CalculateCityQualityOfLife():0.00}", LogCategory.Economy);
            Log($"City Expenses: {city.CityExpenses}", LogCategory.Economy);

            foreach (var suburb in city.Suburbs)
            {
                Log($"  Suburb: {suburb.Name}", LogCategory.Building);
                Log($"    Population: {suburb.Population}", LogCategory.Pop);
                Log($"    Housing Capacity: {suburb.HousingCapacity:0.00}", LogCategory.Building);
                Log($"    Railway Kilometers: {suburb.RailwayKilometers:0.00}", LogCategory.Building);
                Log($"    QoL: {suburb.CalculateQualityOfLife():0.00}", LogCategory.Building);
            }

            foreach (var pop in city.PopClasses)
            {
                Log($"  PopClass: {pop.Name}", LogCategory.Pop);
                Log($"    Size: {pop.Size}", LogCategory.Pop);
                Log($"    Employed: {pop.Employed}", LogCategory.Pop);
                Log($"    Unemployed: {pop.Unemployed}", LogCategory.Pop);
                Log($"    Income Per Person: {pop.IncomePerPerson:0.00}", LogCategory.Pop);
                Log($"    QoL: {pop.QualityOfLife:0.00}", LogCategory.Pop);
                Log($"    Happiness: {pop.Happiness}", LogCategory.Pop);
                Log($"    Needs:");
                foreach (var need in pop.Needs)
                {
                    Log($"      {need.Key}: {need.Value}", LogCategory.Pop);
                }
                Log($"    Unmet Needs: {pop.UnmetNeeds}", LogCategory.Pop);
            }

            foreach (var good in city.LocalSupply.Keys)
            {
                double price = city.LocalPrices.ContainsKey(good) ? city.LocalPrices[good] : 0;
                Log($"  Good: {good}", LogCategory.Economy);
                Log($"    Supply: {city.LocalSupply[good]}", LogCategory.Economy);
                Log($"    Demand: {city.LocalDemand[good]}", LogCategory.Economy);
                Log($"    Price: {price:0.00}", LogCategory.Economy);
            }

            Log($"--- End of Detailed Data for City: {city.Name} ---", LogCategory.Economy);
        }

        public static void LogCountryData(Country country)
        {
            if (country == null) return;

            Log($"Country: {country.Name}", LogCategory.Economy);
            Log($"Budget: {country.Budget}", LogCategory.Economy);

            foreach (var state in country.States)
            {
                Log($"  State: {state.Name}, Budget: {state.Budget}", LogCategory.Economy);

                foreach (var city in state.Cities)
                {
                    LogCityData(city);
                }
            }
        }

        public static void LogFinancialData(NationalFinancialSystem fs, string countryName)
        {
            if (fs == null) return;

            Log($"-- Financial Stats for {countryName} --", LogCategory.Economy);
            Log($"Money Supply: {fs.MoneySupply:C}", LogCategory.Economy);
            Log($"Reserves: {fs.NationalReserves:C}", LogCategory.Economy);
            Log($"Base Rate: {fs.BaseInterestRate:P}", LogCategory.Economy);
            Log($"Outstanding Debt: {fs.GetTotalOutstandingDebt():C}", LogCategory.Economy);
            Log($"Debt/GDP: {fs.DebtToGdpRatio:P}", LogCategory.Economy);
            Log($"Inflation: {fs.InflationRate:P}", LogCategory.Economy);
            Log($"Credit Rating: {fs.CreditRating:P}", LogCategory.Economy);
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
