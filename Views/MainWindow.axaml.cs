using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Runtime.InteropServices;

namespace Economy_sim
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool AllocConsole();
            AllocConsole();
            var newGameButton = this.FindControl<Button>("NewGameButton");
            if (newGameButton != null)
            {
                newGameButton.Click += NewGameButton_Click;
            }

            var loadGameButton = this.FindControl<Button>("LoadGameButton");
            if (loadGameButton != null)
            {
                loadGameButton.Click += LoadGameButton_Click;
            }

            var optionsButton = this.FindControl<Button>("OptionsButton");
            if (optionsButton != null)
            {
                optionsButton.Click += OptionsButton_Click;
            }

            var exitButton = this.FindControl<Button>("ExitButton");
            if (exitButton != null)
            {
                exitButton.Click += ExitButton_Click;
            }
        }

        // --- Event Handlers for Button Clicks ---

        private void NewGameButton_Click(object? sender, RoutedEventArgs e)
        {
            // 1. Create the new window
            var gameWindow = new GameView();

            // 2. Show the new window
            gameWindow.Show();

            // 3. Close this window (the main menu)
            this.Close();
        }

        private void LoadGameButton_Click(object? sender, RoutedEventArgs e)
        {
            Console.WriteLine("Load Game button clicked!");
        }

        private void OptionsButton_Click(object? sender, RoutedEventArgs e)
        {
            // Create and show the options window
            var optionsWindow = new OptionsWindow();
            optionsWindow.Show();

            // Close this main menu window
            this.Close();
        }

        private void ExitButton_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}