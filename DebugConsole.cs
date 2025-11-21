using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Economy_sim
{
    /// <summary>
    /// Debug console for executing commands to open menus and capture screenshots
    /// </summary>
    public class DebugConsole
    {
        private readonly Window _parentWindow;
        private readonly Dictionary<string, Type> _menuTypes;
        private const string ScreenshotsFolder = "screenshots";
        private const int WindowRenderDelayMs = 500;
        private const int ScreenshotRenderDelayMs = 100;

        public DebugConsole(Window parentWindow)
        {
            _parentWindow = parentWindow;
            
            // Map menu names to their window types
            _menuTypes = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                { "main", typeof(MainWindow) },
                { "options", typeof(OptionsWindow) },
                { "loading", typeof(LoadingWindow) },
                { "mapeditor", typeof(MapEditorWindow) },
                { "construction", typeof(ConstructionWindow) },
                { "diplomatic", typeof(DiplomaticRelationsWindow) },
                { "diplomacy", typeof(DiplomaticRelationsWindow) },
                { "factory", typeof(FactoryStatsWindow) },
                { "factorystats", typeof(FactoryStatsWindow) },
                { "performance", typeof(PerformanceStatsWindow) },
                { "performancestats", typeof(PerformanceStatsWindow) },
                { "policy", typeof(PolicyManagerWindow) },
                { "policymanager", typeof(PolicyManagerWindow) },
                { "popstats", typeof(PopStatsWindow) },
                { "population", typeof(PopStatsWindow) },
                { "trade", typeof(TradeManagementWindow) },
                { "trademanagement", typeof(TradeManagementWindow) },
                { "tradeproposal", typeof(TradeProposalWindow) }
            };

            EnsureScreenshotsFolderExists();
        }

        /// <summary>
        /// Execute a debug command
        /// </summary>
        public async Task<string> ExecuteCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return "No command entered.";
            }

            var parts = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "No command entered.";
            }

            var cmd = parts[0].ToLowerInvariant();

            switch (cmd)
            {
                case "help":
                    return GetHelpText();

                case "openmenu":
                case "open":
                    if (parts.Length < 2)
                    {
                        return "Usage: openmenu <menuname>\nAvailable menus: " + 
                               string.Join(", ", _menuTypes.Keys.Distinct().OrderBy(k => k));
                    }
                    return await OpenMenu(parts[1]);

                case "screenshot":
                case "snap":
                    if (parts.Length < 2)
                    {
                        return await CaptureScreenshot(_parentWindow, "current");
                    }
                    return await OpenMenuAndScreenshot(parts[1]);

                case "list":
                case "menus":
                    return "Available menus:\n  " + 
                           string.Join("\n  ", _menuTypes.Keys.Distinct().OrderBy(k => k));

                default:
                    return $"Unknown command: {cmd}\nType 'help' for available commands.";
            }
        }

        private string GetHelpText()
        {
            return @"Debug Console Commands:
  help                    - Show this help text
  openmenu <name>         - Open a menu/window by name
  screenshot <name>       - Open menu and capture screenshot
  screenshot              - Capture screenshot of current window
  list                    - List all available menus

Available menu names:
  " + string.Join("\n  ", _menuTypes.Keys.Distinct().OrderBy(k => k)) + @"

Examples:
  openmenu options
  screenshot factory
  screenshot";
        }

        private async Task<string> OpenMenu(string menuName)
        {
            if (!_menuTypes.TryGetValue(menuName, out var menuType))
            {
                return $"Unknown menu: {menuName}\nAvailable menus: " + 
                       string.Join(", ", _menuTypes.Keys.Distinct().OrderBy(k => k));
            }

            try
            {
                var window = (Window?)Activator.CreateInstance(menuType);
                if (window == null)
                {
                    return $"Failed to create window of type {menuType.Name}";
                }

                window.Show();
                return $"Opened {menuType.Name}";
            }
            catch (Exception ex)
            {
                return $"Error opening menu: {ex.Message}";
            }
        }

        private async Task<string> OpenMenuAndScreenshot(string menuName)
        {
            if (!_menuTypes.TryGetValue(menuName, out var menuType))
            {
                return $"Unknown menu: {menuName}\nAvailable menus: " + 
                       string.Join(", ", _menuTypes.Keys.Distinct().OrderBy(k => k));
            }

            try
            {
                var window = (Window?)Activator.CreateInstance(menuType);
                if (window == null)
                {
                    return $"Failed to create window of type {menuType.Name}";
                }

                window.Show();

                // Wait for window to render
                await Task.Delay(WindowRenderDelayMs);

                // Capture screenshot
                var screenshotResult = await CaptureScreenshot(window, menuName);

                return $"Opened {menuType.Name}\n{screenshotResult}";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private async Task<string> CaptureScreenshot(Window window, string name)
        {
            try
            {
                // Wait a bit to ensure the window is fully rendered
                await Task.Delay(ScreenshotRenderDelayMs);

                var pixelSize = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
                var size = new Size(window.Bounds.Width, window.Bounds.Height);

                using var renderTarget = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
                renderTarget.Render(window);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"{name}_{timestamp}.png";
                var filepath = Path.Combine(ScreenshotsFolder, filename);

                renderTarget.Save(filepath);

                return $"Screenshot saved to: {filepath}";
            }
            catch (Exception ex)
            {
                return $"Failed to capture screenshot: {ex.Message}";
            }
        }

        private void EnsureScreenshotsFolderExists()
        {
            try
            {
                if (!Directory.Exists(ScreenshotsFolder))
                {
                    Directory.CreateDirectory(ScreenshotsFolder);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create screenshots folder: {ex.Message}");
            }
        }
    }
}
