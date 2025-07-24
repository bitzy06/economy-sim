using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;

namespace StrategyGame
{
    public static class DialogHelper
    {
        public static async Task ShowMessage(string message, string title)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow != null)
                {
                    var dialog = new Window
                    {
                        Title = title,
                        Width = 400,
                        Height = 200,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        CanResize = false
                    };

                    var stackPanel = new StackPanel
                    {
                        Margin = new Avalonia.Thickness(20)
                    };

                    var textBlock = new TextBlock
                    {
                        Text = message,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        Margin = new Avalonia.Thickness(0, 0, 0, 20)
                    };

                    var button = new Button
                    {
                        Content = "OK",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        MinWidth = 80
                    };

                    button.Click += (s, e) => dialog.Close();

                    stackPanel.Children.Add(textBlock);
                    stackPanel.Children.Add(button);
                    dialog.Content = stackPanel;

                    await dialog.ShowDialog(mainWindow);
                }
            }
        }
    }
}
