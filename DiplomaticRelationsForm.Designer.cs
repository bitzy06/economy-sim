using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class DiplomaticRelationsForm : Form
    {
        private ListView listViewRelations;

        private void InitializeComponent()
        {
            this.Text = "Diplomatic Relations";
            this.Size = new System.Drawing.Size(600, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            listViewRelations = new ListView
            {
                Location = new Point(10, 10),
                Size = new Size(565, 340),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            listViewRelations.Columns.Add("Country", 120);
            listViewRelations.Columns.Add("Current Stance", 120);
            listViewRelations.Columns.Add("Active Trades", 200);
            listViewRelations.Columns.Add("Duration", 100);

            Controls.Add(listViewRelations);
        }
    }
}
