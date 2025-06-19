using System;
using System.Windows.Forms;
using System.Linq;

namespace economy_sim
{
    public partial class TradeProposalForm : Form
    {

        private StrategyGame.Country playerCountry;
        private System.Collections.Generic.List<StrategyGame.Country> allCountries;
        private StrategyGame.DiplomacyManager diplomacyManager;

        public TradeProposalForm(StrategyGame.Country player, System.Collections.Generic.List<StrategyGame.Country> countries, StrategyGame.DiplomacyManager dipManager)
        {
            playerCountry = player;
            allCountries = countries;
            diplomacyManager = dipManager;
            InitializeComponent();
            InitializeControls();
        }

        private void InitializeControls()
        {
            // Populate country combo box with all countries except player's
            comboBoxTargetCountry.Items.Clear();
            foreach (var country in allCountries.Where(c => c != playerCountry))
            {
                comboBoxTargetCountry.Items.Add(country.Name);
            }
            if (comboBoxTargetCountry.Items.Count > 0)
                comboBoxTargetCountry.SelectedIndex = 0;

            // Populate resource combo box with player's available resources
            UpdateResourceList();
        }

        private void UpdateResourceList()
        {
            comboBoxResource.Items.Clear();
            foreach (var resource in playerCountry.Resources.Where(r => r.Value > 0))
            {
                comboBoxResource.Items.Add(resource.Key);
            }
            if (comboBoxResource.Items.Count > 0)
            {
                comboBoxResource.SelectedIndex = 0;
                UpdateAvailableResourceLabel();
            }
        }

        private void UpdateAvailableResourceLabel()
        {
            if (comboBoxResource.SelectedItem != null)
            {
                string resourceName = comboBoxResource.SelectedItem.ToString();
                double available = playerCountry.GetResourceAmount(resourceName);
                labelAvailableResources.Text = $"Available:\n{resourceName}: {available:N2}";
                numericQuantity.Maximum = (decimal)available;
            }
        }

        private void ComboBoxResource_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateAvailableResourceLabel();
        }

        private void ComboBoxTargetCountry_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Could add logic here to show target country's existing trade relationships or resources
        }

        private void ButtonPropose_Click(object sender, EventArgs e)
        {
            if (comboBoxTargetCountry.SelectedItem == null || comboBoxResource.SelectedItem == null)
            {
                MessageBox.Show("Please select both a country and a resource to trade.", "Invalid Selection", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string targetCountryName = comboBoxTargetCountry.SelectedItem.ToString();
            string resourceName = comboBoxResource.SelectedItem.ToString();

            var newTrade = new StrategyGame.TradeAgreement(
                playerCountry.Name,
                targetCountryName,
                resourceName,
                (double)numericQuantity.Value,
                (double)numericPrice.Value,
                (int)numericDuration.Value
            );

            diplomacyManager.ProposeTradeAgreement(newTrade);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
