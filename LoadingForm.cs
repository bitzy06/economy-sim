using System.Windows.Forms;

namespace EconomySim
{
    public partial class LoadingForm : Form
    {
        public LoadingForm()
        {
            InitializeComponent();
        }

        public void UpdateProgress(int processed, int total)
        {
            int percent = total > 0 ? (int)(processed * 100.0 / total) : 0;
            if (percent > 100) percent = 100;
            progressBar.Value = percent;
            labelStatus.Text = $"Generating city models {processed}/{total}";
        }
    }
}
