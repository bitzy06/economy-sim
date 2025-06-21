using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class ConstructionForm : Form
    {
        private ListBox listBoxProjects;
        private ComboBox comboBoxType;
        private NumericUpDown numericCost;
        private NumericUpDown numericDuration;
        private NumericUpDown numericOutput;
        private Label labelType;
        private Label labelCost;
        private Label labelDuration;
        private Label labelOutput;
        private Button buttonStart;

        private void InitializeComponent()
        {
            listBoxProjects = new ListBox();
            comboBoxType = new ComboBox();
            numericCost = new NumericUpDown();
            numericDuration = new NumericUpDown();
            numericOutput = new NumericUpDown();
            labelType = new Label();
            labelCost = new Label();
            labelDuration = new Label();
            labelOutput = new Label();
            buttonStart = new Button();
            SuspendLayout();
            //
            // listBoxProjects
            //
            listBoxProjects.Dock = DockStyle.Top;
            listBoxProjects.Height = 180;
            listBoxProjects.Location = new Point(0, 0);
            listBoxProjects.Name = "listBoxProjects";
            listBoxProjects.Size = new Size(400, 180);
            //
            // labelType
            //
            labelType.AutoSize = true;
            labelType.Text = "Project Type:";
            labelType.Location = new Point(10, 190);
            //
            // comboBoxType
            //
            comboBoxType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxType.Items.AddRange(Enum.GetNames(typeof(ProjectType)));
            comboBoxType.SelectedIndex = 0;
            comboBoxType.Location = new Point(120, 188);
            //
            // labelCost
            //
            labelCost.AutoSize = true;
            labelCost.Text = "Cost:";
            labelCost.Location = new Point(10, 220);
            //
            // numericCost
            //
            numericCost.Minimum = 0;
            numericCost.Maximum = 1000000;
            numericCost.Value = 1000;
            numericCost.Location = new Point(120, 218);
            //
            // labelDuration
            //
            labelDuration.AutoSize = true;
            labelDuration.Text = "Duration (days):";
            labelDuration.Location = new Point(10, 250);
            //
            // numericDuration
            //
            numericDuration.Minimum = 1;
            numericDuration.Maximum = 365;
            numericDuration.Value = 30;
            numericDuration.Location = new Point(120, 248);
            //
            // labelOutput
            //
            labelOutput.AutoSize = true;
            labelOutput.Text = "Output:";
            labelOutput.Location = new Point(10, 280);
            //
            // numericOutput
            //
            numericOutput.Minimum = 1;
            numericOutput.Maximum = 1000;
            numericOutput.Value = 10;
            numericOutput.Location = new Point(120, 278);
            //
            // buttonStart
            //
            buttonStart.Text = "Start Project";
            buttonStart.Location = new Point(10, 320);
            buttonStart.Click += ButtonStart_Click;
            //
            // ConstructionForm
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(400, 400);
            Controls.Add(listBoxProjects);
            Controls.Add(labelType);
            Controls.Add(comboBoxType);
            Controls.Add(labelCost);
            Controls.Add(numericCost);
            Controls.Add(labelDuration);
            Controls.Add(numericDuration);
            Controls.Add(labelOutput);
            Controls.Add(numericOutput);
            Controls.Add(buttonStart);
            Name = "ConstructionForm";
            Text = "City Construction";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
