using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class PopStatsForm : Form
    {
        private ListBox listBoxPopStats;

        private void InitializeComponent()
        {
            listBoxPopStats = new ListBox();
            SuspendLayout();
            // 
            // listBoxPopStats
            //
            listBoxPopStats.Dock = DockStyle.Fill;
            listBoxPopStats.FormattingEnabled = true;
            listBoxPopStats.ItemHeight = 15;
            listBoxPopStats.Location = new Point(0, 0);
            listBoxPopStats.Name = "listBoxPopStats";
            listBoxPopStats.Size = new Size(400, 400);
            listBoxPopStats.TabIndex = 0;

            // 
            // PopStatsForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(400, 400);
            Controls.Add(listBoxPopStats);
            Name = "PopStatsForm";
            Text = "Population Class Stats";
            ResumeLayout(false);
        }
    }
}
