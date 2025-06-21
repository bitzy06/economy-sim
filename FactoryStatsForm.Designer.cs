using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class FactoryStatsForm : Form
    {
        private ListBox listBoxFactoryDetails;

        private void InitializeComponent()
        {
            listBoxFactoryDetails = new ListBox();
            SuspendLayout();
            // 
            // listBoxFactoryDetails
            //
            listBoxFactoryDetails.Dock = DockStyle.Fill;
            listBoxFactoryDetails.FormattingEnabled = true;
            listBoxFactoryDetails.ItemHeight = 15;
            listBoxFactoryDetails.IntegralHeight = false;
            listBoxFactoryDetails.Location = new Point(0, 0);
            listBoxFactoryDetails.Name = "listBoxFactoryDetails";
            listBoxFactoryDetails.Size = new Size(600, 400);
            listBoxFactoryDetails.TabIndex = 0;

            // 
            // FactoryStatsForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(600, 400);
            Controls.Add(listBoxFactoryDetails);
            Name = "FactoryStatsForm";
            Text = "Factory Details";
            ResumeLayout(false);
        }
    }
}
