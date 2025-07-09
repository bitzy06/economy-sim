using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class PerformanceStatsForm : Form
    {
        private DataGridView dataGridView;

        private void InitializeComponent()
        {
            dataGridView = new DataGridView();
            SuspendLayout();

            dataGridView.Dock = DockStyle.Fill;
            dataGridView.AllowUserToAddRows = false;
            dataGridView.AllowUserToDeleteRows = false;
            dataGridView.ReadOnly = true;
            dataGridView.RowHeadersVisible = false;
            dataGridView.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView.Columns.Add("Area", "Area");
            dataGridView.Columns.Add("Count", "Count");
            dataGridView.Columns.Add("AverageMs", "Avg ms");

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(400, 300);
            Controls.Add(dataGridView);
            Name = "PerformanceStatsForm";
            Text = "Performance Stats";
            ResumeLayout(false);
        }
    }
}
