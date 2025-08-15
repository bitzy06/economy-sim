using System;
using System.IO;
using System.Diagnostics;

namespace Economy_sim
{
    /// <summary>
    /// Utility to help users set up state rendering data requirements
    /// </summary>
    public static class StateRenderingSetup
    {
        private static readonly string StatesDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "data", "country_borders", "states", "ne_10m_admin_1_states_provinces.shp");

        /// <summary>
        /// Validates state rendering setup and provides helpful guidance
        /// </summary>
        public static bool ValidateStateRenderingSetup(bool showConsoleOutput = true)
        {
            bool isValid = true;
            var messages = new System.Collections.Generic.List<string>();

            // Check if states directory exists
            string statesDir = Path.GetDirectoryName(StatesDataPath)!;
            if (!Directory.Exists(statesDir))
            {
                isValid = false;
                messages.Add($"❌ States data directory missing: {statesDir}");
                messages.Add($"   Create this directory first.");
            }
            else
            {
                messages.Add($"✅ States data directory exists: {statesDir}");
            }

            // Check for required shapefile components
            string[] requiredFiles = {
                ".shp",   // Main shapefile
                ".shx",   // Spatial index
                ".dbf",   // Attribute data
                ".prj"    // Projection info
            };

            string basePath = Path.ChangeExtension(StatesDataPath, null);
            foreach (string extension in requiredFiles)
            {
                string filePath = basePath + extension;
                if (File.Exists(filePath))
                {
                    messages.Add($"✅ Found: {Path.GetFileName(filePath)}");
                }
                else
                {
                    isValid = false;
                    messages.Add($"❌ Missing: {Path.GetFileName(filePath)}");
                }
            }

            if (!isValid)
            {
                messages.Add("");
                messages.Add("🔧 TO ENABLE STATE RENDERING:");
                messages.Add("1. Download Natural Earth Admin 1 States/Provinces data:");
                messages.Add("   https://www.naturalearthdata.com/http//www.naturalearthdata.com/download/10m/cultural/ne_10m_admin_1_states_provinces.zip");
                messages.Add($"2. Extract all files to: {statesDir}");
                messages.Add("3. Restart the application");
                messages.Add("4. States will render when you select a country and zoom to level 3+");
            }
            else
            {
                messages.Add("");
                messages.Add("🎉 State rendering is properly configured!");
                messages.Add("   Select a country and zoom to level 3+ to see state borders.");
            }

            if (showConsoleOutput)
            {
                foreach (string message in messages)
                {
                    Debug.WriteLine(message);
                    Console.WriteLine(message);
                }
            }

            return isValid;
        }

        /// <summary>
        /// Gets the expected file path for state data
        /// </summary>
        public static string GetStatesDataPath()
        {
            return StatesDataPath;
        }

        /// <summary>
        /// Gets the states data directory
        /// </summary>
        public static string GetStatesDataDirectory()
        {
            return Path.GetDirectoryName(StatesDataPath)!;
        }
    }
}