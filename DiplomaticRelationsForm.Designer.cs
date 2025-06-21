using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class DiplomaticRelationsForm : Form
    {
        private ListView listViewRelations;

        private void InitializeComponent()
        {
            listViewRelations = new ListView();
            SuspendLayout();

            // listViewRelations
            listViewRelations.Location = new Point(10, 10);
            listViewRelations.Size = new Size(565, 340);
            listViewRelations.View = View.Details;
            listViewRelations.FullRowSelect = true;
            listViewRelations.GridLines = true;
            listViewRelations.Columns.Add("Country", 120);
            listViewRelations.Columns.Add("Current Stance", 120);
            listViewRelations.Columns.Add("Active Trades", 200);
            listViewRelations.Columns.Add("Duration", 100);

            // DiplomaticRelationsForm
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(600, 400);
            Controls.Add(listViewRelations);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "DiplomaticRelationsForm";
            StartPosition = FormStartPosition.CenterParent;
            Text = "Diplomatic Relations";
            ResumeLayout(false);
        }
    }
}
