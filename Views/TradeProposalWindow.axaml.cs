using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;

namespace Economy_sim
{
    public partial class TradeProposalWindow : Window
    {
        private bool _isExport;

        public TradeProposalWindow()
        {
            InitializeComponent();
            ConfigureControls();
        }

        public void Configure(bool isExport, IEnumerable<string> countries, IEnumerable<string> goods, string? defaultFrom, string? defaultTo)
        {
            _isExport = isExport;
            DirectionText.Text = isExport
                ? "Create an export agreement from your country to a partner."
                : "Create an import agreement to bring goods into your country.";

            var countryList = countries.ToList();
            FromCountryComboBox.Items = countryList;
            ToCountryComboBox.Items = countryList;
            var goodsList = goods.ToList();
            ResourceComboBox.Items = goodsList;

            if (!string.IsNullOrWhiteSpace(defaultFrom) && countryList.Contains(defaultFrom))
            {
                FromCountryComboBox.SelectedItem = defaultFrom;
            }

            if (!string.IsNullOrWhiteSpace(defaultTo) && countryList.Contains(defaultTo))
            {
                ToCountryComboBox.SelectedItem = defaultTo;
            }

            if (goodsList.Count > 0)
            {
                ResourceComboBox.SelectedIndex = 0;
            }
        }

        private void ConfigureControls()
        {
            if (TariffTypeComboBox != null)
            {
                TariffTypeComboBox.Items = Enum.GetValues(typeof(TariffType)).Cast<TariffType>().ToList();
                TariffTypeComboBox.SelectedItem = TariffType.None;
            }

            if (DurationTextBox != null)
            {
                DurationTextBox.Text = "12";
            }

            if (TariffRateTextBox != null)
            {
                TariffRateTextBox.Text = "0";
            }

            if (SubmitButton != null)
            {
                SubmitButton.Click += (_, __) => Submit();
            }

            if (CancelButton != null)
            {
                CancelButton.Click += (_, __) => Close(null);
            }
        }

        private void Submit()
        {
            if (!double.TryParse(QuantityTextBox.Text, out var quantity) || quantity <= 0)
            {
                ValidationText.Text = "Enter a valid quantity.";
                return;
            }

            if (!double.TryParse(PriceTextBox.Text, out var price) || price <= 0)
            {
                ValidationText.Text = "Enter a valid price.";
                return;
            }

            if (!int.TryParse(DurationTextBox.Text, out var duration) || duration <= 0)
            {
                ValidationText.Text = "Enter a valid duration.";
                return;
            }

            if (!double.TryParse(TariffRateTextBox.Text, out var tariffRate) || tariffRate < 0)
            {
                ValidationText.Text = "Enter a valid tariff rate.";
                return;
            }

            var fromCountry = FromCountryComboBox.SelectedItem as string ?? FromCountryComboBox.Text;
            var toCountry = ToCountryComboBox.SelectedItem as string ?? ToCountryComboBox.Text;
            var resource = ResourceComboBox.SelectedItem as string ?? ResourceComboBox.Text;

            if (string.IsNullOrWhiteSpace(fromCountry) || string.IsNullOrWhiteSpace(toCountry) || string.IsNullOrWhiteSpace(resource))
            {
                ValidationText.Text = "Please provide from/to countries and a resource.";
                return;
            }

            var parameters = new TradeDealParameters
            {
                IsExport = _isExport,
                FromCountry = fromCountry,
                ToCountry = toCountry,
                Resource = resource,
                Quantity = quantity,
                Price = price,
                Duration = duration,
                TariffRate = tariffRate,
                TariffType = TariffTypeComboBox.SelectedItem is TariffType tariff ? tariff : TariffType.None
            };

            Close(parameters);
        }
    }
}
