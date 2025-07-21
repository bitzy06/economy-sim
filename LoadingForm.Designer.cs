using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class LoadingForm : Form
    {
        private ProgressBar progressBar;
        private Label labelStatus;

        private void InitializeComponent()
        {
            progressBar = new ProgressBar();
            labelStatus = new Label();
            SuspendLayout();

            progressBar.Dock = DockStyle.Bottom;
            progressBar.Minimum = 0;
            progressBar.Maximum = 100;
            progressBar.Style = ProgressBarStyle.Continuous;

            labelStatus.Dock = DockStyle.Fill;
            labelStatus.TextAlign = ContentAlignment.MiddleCenter;

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(300, 80);
            Controls.Add(labelStatus);
            Controls.Add(progressBar);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Loading";
            ControlBox = false;

            ResumeLayout(false);
        }
    }
}
