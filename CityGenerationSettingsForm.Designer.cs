using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class CityGenerationSettingsForm : Form
    {
        private CheckedListBox checkedListBoxCountries;
        private Button buttonGenerate;
        private Button buttonCancel;
        private Label labelInfo;

        private void InitializeComponent()
        {
            checkedListBoxCountries = new CheckedListBox();
            buttonGenerate = new Button();
            buttonCancel = new Button();
            labelInfo = new Label();
            SuspendLayout();
            //
            // labelInfo
            //
            labelInfo.AutoSize = true;
            labelInfo.Text = "Select countries for city generation:";
            labelInfo.Location = new Point(12, 9);
            //
            // checkedListBoxCountries
            //
            checkedListBoxCountries.CheckOnClick = true;
            checkedListBoxCountries.FormattingEnabled = true;
            checkedListBoxCountries.Location = new Point(12, 35);
            checkedListBoxCountries.Size = new Size(220, 184);
            //
            // buttonGenerate
            //
            buttonGenerate.Text = "Generate";
            buttonGenerate.Location = new Point(12, 230);
            buttonGenerate.Click += ButtonGenerate_Click;
            //
            // buttonCancel
            //
            buttonCancel.Text = "Cancel";
            buttonCancel.Location = new Point(110, 230);
            buttonCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            //
            // CityGenerationSettingsForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(244, 270);
            Controls.Add(labelInfo);
            Controls.Add(checkedListBoxCountries);
            Controls.Add(buttonGenerate);
            Controls.Add(buttonCancel);
            Name = "CityGenerationSettingsForm";
            Text = "City Data Options";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
