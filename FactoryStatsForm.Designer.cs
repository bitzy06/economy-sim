using System.Windows.Forms;

namespace economy_sim
{
    public partial class FactoryStatsForm : Form
    {
        private ListBox listBoxFactoryDetails;

        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 400);
            this.Name = "FactoryStatsForm";
            this.Text = "Factory Details";

            listBoxFactoryDetails = new ListBox();
            listBoxFactoryDetails.Dock = DockStyle.Fill;
            listBoxFactoryDetails.FormattingEnabled = true;
            listBoxFactoryDetails.ItemHeight = 15;
            listBoxFactoryDetails.IntegralHeight = false;
            this.Controls.Add(listBoxFactoryDetails);

            this.ResumeLayout(false);
        }
    }
}
