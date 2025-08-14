using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.IO;
using System.Threading.Tasks;
using StrategyGame;

namespace Economy_sim
{
    public partial class OptionsWindow : Window
    {
        private PoliticalBorderManager? _politicalManager;
        
        public OptionsWindow()
        {
            InitializeComponent();

            var processCountriesButton = this.FindControl<Button>("ProcessCountriesButton");
            if (processCountriesButton != null)
            {
                processCountriesButton.Click += ProcessCountriesButton_Click;
            }

            var backToMainMenuButton = this.FindControl<Button>("BackToMainMenuButton");
            if (backToMainMenuButton != null)
            {
                backToMainMenuButton.Click += BackToMainMenuButton_Click;
            }
        }

        private async void ProcessCountriesButton_Click(object? sender, RoutedEventArgs e)
        {
            var statusText = this.FindControl<TextBlock>("StatusText");
            var processButton = this.FindControl<Button>("ProcessCountriesButton");
            
            if (statusText == null || processButton == null) return;

            try
            {
                // Disable button during processing
                processButton.IsEnabled = false;
                statusText.Text = "🔄 Processing CShapes-2.0 data for 1950 countries...";

                // Run the processing in a background task
                await Task.Run(() => ProcessCountriesFromCShapes());

                statusText.Text = "✅ Successfully processed 1950 countries!\n" + 
                                 "📁 Data saved to Documents\\data\\country_borders\\country_cache_1950.json\n" +
                                 "🌍 Countries are now available for the political map system";
            }
            catch (FileNotFoundException ex)
            {
                statusText.Text = $"❌ CShapes file not found:\n{ex.Message}";
            }
            catch (Exception ex)
            {
                statusText.Text = $"❌ Error processing countries: {ex.Message}\n\n" +
                                 "Please check that the CShapes-2.0.shp file and its associated files " +
                                 "(.shx, .dbf, .prj) are in Documents\\data\\country_borders\\";
            }
            finally
            {
                // Re-enable button
                if (processButton != null)
                    processButton.IsEnabled = true;
            }
        }

        private void ProcessCountriesFromCShapes()
        {
            // Create the political border manager which will handle the 1950 processing
            _politicalManager = new PoliticalBorderManager();

            // Define the path to CShapes data file
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string cshapesPath = Path.Combine(documentsPath, "data", "country_borders", "CShapes-2.0.shp");

            // Check if CShapes file exists
            if (!File.Exists(cshapesPath))
            {
                throw new FileNotFoundException(
                    $"CShapes-2.0.shp file not found at: {cshapesPath}\n\n" +
                    "Please place the CShapes-2.0.shp file and its associated files (.shx, .dbf, .prj) " +
                    "in the Documents\\data\\country_borders\\ directory.");
            }

            // Force cache generation by calling the method that actually triggers the processing
            // This will ensure the 1950 country data is generated and cached properly
            _politicalManager.GenerateCountryCacheForced(cshapesPath);
        }

        private void BackToMainMenuButton_Click(object? sender, RoutedEventArgs e)
        {
            // Create and show the main menu window
            var mainWindow = new MainWindow();
            mainWindow.Show();

            // Close this options window
            this.Close();
        }
    }
}