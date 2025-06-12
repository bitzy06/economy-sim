using System;
using System.Windows.Forms;
using System.Linq;

namespace economy_sim
{
    public partial class TradeProposalForm : Form
    {
        private ComboBox comboBoxTargetCountry;
        private ComboBox comboBoxResource;
        private NumericUpDown numericQuantity;
        private NumericUpDown numericPrice;
        private NumericUpDown numericDuration;
        private Button buttonPropose;
        private Button buttonCancel;
        private Label labelCountry;
        private Label labelResource;
        private Label labelQuantity;
        private Label labelPrice;
        private Label labelDuration;
        private Label labelAvailableResources;

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

        private void InitializeComponent()
        {
            this.Text = "Propose Trade Agreement";
            this.Size = new System.Drawing.Size(400, 350);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            labelCountry = new Label
            {
                Text = "Trade with Country:",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(150, 20)
            };

            comboBoxTargetCountry = new ComboBox
            {
                Location = new System.Drawing.Point(20, 40),
                Size = new System.Drawing.Size(200, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            labelResource = new Label
            {
                Text = "Resource to Trade:",
                Location = new System.Drawing.Point(20, 70),
                Size = new System.Drawing.Size(150, 20)
            };

            comboBoxResource = new ComboBox
            {
                Location = new System.Drawing.Point(20, 90),
                Size = new System.Drawing.Size(200, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            labelQuantity = new Label
            {
                Text = "Quantity:",
                Location = new System.Drawing.Point(20, 120),
                Size = new System.Drawing.Size(150, 20)
            };

            numericQuantity = new NumericUpDown
            {
                Location = new System.Drawing.Point(20, 140),
                Size = new System.Drawing.Size(120, 20),
                Minimum = 1,
                Maximum = 10000,
                Value = 1
            };

            labelPrice = new Label
            {
                Text = "Price per Unit:",
                Location = new System.Drawing.Point(20, 170),
                Size = new System.Drawing.Size(150, 20)
            };

            numericPrice = new NumericUpDown
            {
                Location = new System.Drawing.Point(20, 190),
                Size = new System.Drawing.Size(120, 20),
                Minimum = 1,
                Maximum = 10000,
                Value = 100,
                DecimalPlaces = 2
            };

            labelDuration = new Label
            {
                Text = "Duration (turns):",
                Location = new System.Drawing.Point(20, 220),
                Size = new System.Drawing.Size(150, 20)
            };

            numericDuration = new NumericUpDown
            {
                Location = new System.Drawing.Point(20, 240),
                Size = new System.Drawing.Size(120, 20),
                Minimum = 1,
                Maximum = 100,
                Value = 5
            };

            labelAvailableResources = new Label
            {
                Text = "Available Resources:",
                Location = new System.Drawing.Point(230, 90),
                Size = new System.Drawing.Size(150, 100),
                AutoSize = false
            };

            buttonPropose = new Button
            {
                Text = "Propose Trade",
                Location = new System.Drawing.Point(20, 280),
                Size = new System.Drawing.Size(100, 25)
            };

            buttonCancel = new Button
            {
                Text = "Cancel",
                Location = new System.Drawing.Point(130, 280),
                Size = new System.Drawing.Size(100, 25),
                DialogResult = DialogResult.Cancel
            };

            Controls.AddRange(new Control[] {
                labelCountry, comboBoxTargetCountry,
                labelResource, comboBoxResource,
                labelQuantity, numericQuantity,
                labelPrice, numericPrice,
                labelDuration, numericDuration,
                labelAvailableResources,
                buttonPropose, buttonCancel
            });

            buttonPropose.Click += ButtonPropose_Click;
            comboBoxResource.SelectedIndexChanged += ComboBoxResource_SelectedIndexChanged;
            comboBoxTargetCountry.SelectedIndexChanged += ComboBoxTargetCountry_SelectedIndexChanged;
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
