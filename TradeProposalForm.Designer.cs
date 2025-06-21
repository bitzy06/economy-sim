using System.Drawing;
using System.Windows.Forms;

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
            labelCountry = new Label();
            comboBoxTargetCountry = new ComboBox();
            labelResource = new Label();
            comboBoxResource = new ComboBox();
            labelQuantity = new Label();
            numericQuantity = new NumericUpDown();
            labelPrice = new Label();
            numericPrice = new NumericUpDown();
            labelDuration = new Label();
            numericDuration = new NumericUpDown();
            labelAvailableResources = new Label();
            buttonPropose = new Button();
            buttonCancel = new Button();
            SuspendLayout();

            // labelCountry
            labelCountry.Text = "Trade with Country:";
            labelCountry.Location = new Point(20, 20);
            labelCountry.Size = new Size(150, 20);

            // comboBoxTargetCountry
            comboBoxTargetCountry.Location = new Point(20, 40);
            comboBoxTargetCountry.Size = new Size(200, 20);
            comboBoxTargetCountry.DropDownStyle = ComboBoxStyle.DropDownList;

            // labelResource
            labelResource.Text = "Resource to Trade:";
            labelResource.Location = new Point(20, 70);
            labelResource.Size = new Size(150, 20);

            // comboBoxResource
            comboBoxResource.Location = new Point(20, 90);
            comboBoxResource.Size = new Size(200, 20);
            comboBoxResource.DropDownStyle = ComboBoxStyle.DropDownList;

            // labelQuantity
            labelQuantity.Text = "Quantity:";
            labelQuantity.Location = new Point(20, 120);
            labelQuantity.Size = new Size(150, 20);

            // numericQuantity
            numericQuantity.Location = new Point(20, 140);
            numericQuantity.Size = new Size(120, 20);
            numericQuantity.Minimum = 1;
            numericQuantity.Maximum = 10000;
            numericQuantity.Value = 1;

            // labelPrice
            labelPrice.Text = "Price per Unit:";
            labelPrice.Location = new Point(20, 170);
            labelPrice.Size = new Size(150, 20);

            // numericPrice
            numericPrice.Location = new Point(20, 190);
            numericPrice.Size = new Size(120, 20);
            numericPrice.Minimum = 1;
            numericPrice.Maximum = 10000;
            numericPrice.Value = 100;
            numericPrice.DecimalPlaces = 2;

            // labelDuration
            labelDuration.Text = "Duration (turns):";
            labelDuration.Location = new Point(20, 220);
            labelDuration.Size = new Size(150, 20);

            // numericDuration
            numericDuration.Location = new Point(20, 240);
            numericDuration.Size = new Size(120, 20);
            numericDuration.Minimum = 1;
            numericDuration.Maximum = 100;
            numericDuration.Value = 5;

            // labelAvailableResources
            labelAvailableResources.Text = "Available Resources:";
            labelAvailableResources.Location = new Point(230, 90);
            labelAvailableResources.Size = new Size(150, 100);
            labelAvailableResources.AutoSize = false;

            // buttonPropose
            buttonPropose.Text = "Propose Trade";
            buttonPropose.Location = new Point(20, 280);
            buttonPropose.Size = new Size(100, 25);
            buttonPropose.Click += ButtonPropose_Click;

            // buttonCancel
            buttonCancel.Text = "Cancel";
            buttonCancel.Location = new Point(130, 280);
            buttonCancel.Size = new Size(100, 25);
            buttonCancel.DialogResult = DialogResult.Cancel;

            Controls.AddRange(new Control[]
            {
                labelCountry, comboBoxTargetCountry,
                labelResource, comboBoxResource,
                labelQuantity, numericQuantity,
                labelPrice, numericPrice,
                labelDuration, numericDuration,
                labelAvailableResources,
                buttonPropose, buttonCancel
            });

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(400, 350);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "TradeProposalForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Propose Trade Agreement";
            ResumeLayout(false);
        }
    }
}
