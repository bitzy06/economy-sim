using System.Windows.Forms;

namespace economy_sim
{
    public partial class PopStatsForm : Form
    {
        private ListBox listBoxPopStats;

        private void InitializeComponent()
        {
            this.Text = "Population Class Stats";
            this.Size = new System.Drawing.Size(400, 400);
            listBoxPopStats = new ListBox();
            listBoxPopStats.Dock = DockStyle.Fill;
            this.Controls.Add(listBoxPopStats);
        }
    }
}
