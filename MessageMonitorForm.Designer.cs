using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class MessageMonitorForm : Form
    {
        private ListBox listBoxEvents;
        private TextBox textBoxMessage;
        private Button buttonPublish;

        private void InitializeComponent()
        {
            listBoxEvents = new ListBox();
            textBoxMessage = new TextBox();
            buttonPublish = new Button();
            SuspendLayout();

            // listBoxEvents
            listBoxEvents.Dock = DockStyle.Top;
            listBoxEvents.Height = 180;
            listBoxEvents.FormattingEnabled = true;

            // textBoxMessage
            textBoxMessage.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            textBoxMessage.Location = new Point(10, 190);
            textBoxMessage.Size = new Size(260, 23);

            // buttonPublish
            buttonPublish.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonPublish.Location = new Point(280, 188);
            buttonPublish.Size = new Size(80, 27);
            buttonPublish.Text = "Publish";
            buttonPublish.Click += ButtonPublish_Click;

            // MessageMonitorForm
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(370, 230);
            Controls.Add(listBoxEvents);
            Controls.Add(textBoxMessage);
            Controls.Add(buttonPublish);
            Name = "MessageMonitorForm";
            Text = "Message Monitor";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
