using System.Windows.Forms;
using System.Drawing;

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

        private void InitializeComponent()
        {
            this.Text = "Propose Trade Agreement";
            this.Size = new Size(400, 350);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            labelCountry = new Label
            {
                Text = "Trade with Country:",
                Location = new Point(20, 20),
                Size = new Size(150, 20)
            };

            comboBoxTargetCountry = new ComboBox
            {
                Location = new Point(20, 40),
                Size = new Size(200, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            labelResource = new Label
            {
                Text = "Resource to Trade:",
                Location = new Point(20, 70),
                Size = new Size(150, 20)
            };

            comboBoxResource = new ComboBox
            {
                Location = new Point(20, 90),
                Size = new Size(200, 20),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            labelQuantity = new Label
            {
                Text = "Quantity:",
                Location = new Point(20, 120),
                Size = new Size(150, 20)
            };

            numericQuantity = new NumericUpDown
            {
                Location = new Point(20, 140),
                Size = new Size(120, 20),
                Minimum = 1,
                Maximum = 10000,
                Value = 1
            };

            labelPrice = new Label
            {
                Text = "Price per Unit:",
                Location = new Point(20, 170),
                Size = new Size(150, 20)
            };

            numericPrice = new NumericUpDown
            {
                Location = new Point(20, 190),
                Size = new Size(120, 20),
                Minimum = 1,
                Maximum = 10000,
                Value = 100,
                DecimalPlaces = 2
            };

            labelDuration = new Label
            {
                Text = "Duration (turns):",
                Location = new Point(20, 220),
                Size = new Size(150, 20)
            };

            numericDuration = new NumericUpDown
            {
                Location = new Point(20, 240),
                Size = new Size(120, 20),
                Minimum = 1,
                Maximum = 100,
                Value = 5
            };

            labelAvailableResources = new Label
            {
                Text = "Available Resources:",
                Location = new Point(230, 90),
                Size = new Size(150, 100),
                AutoSize = false
            };

            buttonPropose = new Button
            {
                Text = "Propose Trade",
                Location = new Point(20, 280),
                Size = new Size(100, 25)
            };

            buttonCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(130, 280),
                Size = new Size(100, 25),
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
    }
}
