using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class EventViewer : Form
    {
        private ListBox eventsList;
        private TrackBar scrubBar;
        private Button buttonOpen;

        private void InitializeComponent()
        {
            eventsList = new ListBox();
            scrubBar = new TrackBar();
            buttonOpen = new Button();
            SuspendLayout();

            // eventsList
            eventsList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            eventsList.FormattingEnabled = true;
            eventsList.ItemHeight = 15;
            eventsList.Location = new Point(12, 41);
            eventsList.Size = new Size(360, 274);

            // scrubBar
            scrubBar.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            scrubBar.Location = new Point(12, 321);
            scrubBar.Size = new Size(360, 45);

            // buttonOpen
            buttonOpen.Location = new Point(12, 12);
            buttonOpen.Size = new Size(75, 23);
            buttonOpen.Text = "Open";
            buttonOpen.UseVisualStyleBackColor = true;

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(384, 361);
            Controls.Add(buttonOpen);
            Controls.Add(eventsList);
            Controls.Add(scrubBar);
            Name = "EventViewer";
            Text = "Event Viewer";

            ResumeLayout(false);
            PerformLayout();
        }
    }
}
