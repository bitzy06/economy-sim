using System;
using System.Windows.Forms;
using System.Linq;

namespace EconomySim
{
    public partial class PerformanceStatsForm : Form
    {
        private readonly System.Windows.Forms.Timer refreshTimer;

        public PerformanceStatsForm()
        {
            InitializeComponent();
            refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            refreshTimer.Tick += (s, e) => UpdateStats();
            refreshTimer.Start();
        }

        private void UpdateStats()
        {
            var stats = PerformanceTracker.GetStats().ToList();
            dataGridView.Rows.Clear();
            foreach (var (area, count, avg) in stats)
            {
                dataGridView.Rows.Add(area, count, avg.ToString("0.00"));
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            Hide();
        }
    }
}
