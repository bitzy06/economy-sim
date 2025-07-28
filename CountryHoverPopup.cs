using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using StrategyGame;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Economy_sim
{
    /// <summary>
    /// Manages the country hover popup functionality for the political map view
    /// </summary>
    public class CountryHoverPopup
    {
        private readonly Border _popupBorder;
        private readonly StackPanel _contentPanel;
        private readonly TextBlock _countryNameText;
        private readonly TextBlock _populationText;
        private readonly TextBlock _budgetText;
        private readonly TextBlock _resourcesText;
        private Country? _currentCountry;
        private bool _isVisible = false;

        public CountryHoverPopup()
        {
            // Create the popup UI elements
            _countryNameText = new TextBlock
            {
                FontWeight = FontWeight.Bold,
                FontSize = 16,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 5)
            };

            _populationText = new TextBlock
            {
                FontSize = 12,
                Foreground = Brushes.LightGray,
                Margin = new Thickness(0, 2)
            };

            _budgetText = new TextBlock
            {
                FontSize = 12,
                Foreground = Brushes.LightGreen,
                Margin = new Thickness(0, 2)
            };

            _resourcesText = new TextBlock
            {
                FontSize = 12,
                Foreground = Brushes.LightBlue,
                Margin = new Thickness(0, 2),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 200
            };

            _contentPanel = new StackPanel
            {
                Children = { _countryNameText, _populationText, _budgetText, _resourcesText },
                Margin = new Thickness(10)
            };

            _popupBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 40, 40, 40)), // Semi-transparent dark background
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Child = _contentPanel,
                IsVisible = false,
                ZIndex = 10000 // Ensure it appears above everything
            };
        }

        /// <summary>
        /// Gets the popup border control that should be added to the main UI
        /// </summary>
        public Border PopupControl => _popupBorder;

        /// <summary>
        /// Shows the popup with information about the specified country
        /// </summary>
        /// <param name="country">The country to display information for</param>
        /// <param name="position">The position where to show the popup</param>
        public void ShowCountryInfo(Country country, Point position)
        {
            if (country == null) return;

            _currentCountry = country;
            UpdatePopupContent(country);
            PositionPopup(position);
            
            if (!_isVisible)
            {
                _popupBorder.IsVisible = true;
                _isVisible = true;
            }
        }

        /// <summary>
        /// Hides the popup
        /// </summary>
        public void Hide()
        {
            if (_isVisible)
            {
                _popupBorder.IsVisible = false;
                _isVisible = false;
                _currentCountry = null;
            }
        }

        /// <summary>
        /// Updates the popup position
        /// </summary>
        /// <param name="position">New position for the popup</param>
        public void UpdatePosition(Point position)
        {
            if (_isVisible)
            {
                PositionPopup(position);
            }
        }

        /// <summary>
        /// Checks if the popup is currently showing information for the specified country
        /// </summary>
        public bool IsShowingCountry(Country country)
        {
            return _isVisible && _currentCountry == country;
        }

        private void UpdatePopupContent(Country country)
        {
            _countryNameText.Text = country.Name;
            _populationText.Text = $"👥 Population: {country.Population:N0}";
            _budgetText.Text = $"💰 Budget: ${country.Budget:N0}";

            // Format resources information
            if (country.Resources.Any())
            {
                var resourceList = country.Resources
                    .Take(3) // Show only first 3 resources to keep popup compact
                    .Select(r => $"{r.Key}: {r.Value:N0}")
                    .ToList();
                
                var resourceText = "🏭 Resources: " + string.Join(", ", resourceList);
                if (country.Resources.Count > 3)
                {
                    resourceText += $" (+{country.Resources.Count - 3} more)";
                }
                _resourcesText.Text = resourceText;
                _resourcesText.IsVisible = true;
            }
            else
            {
                _resourcesText.IsVisible = false;
            }
        }

        private void PositionPopup(Point position)
        {
            // Offset the popup slightly from the cursor to avoid interfering with mouse events
            const double offsetX = 15;
            const double offsetY = 15;

            Canvas.SetLeft(_popupBorder, position.X + offsetX);
            Canvas.SetTop(_popupBorder, position.Y + offsetY);
        }
    }
}