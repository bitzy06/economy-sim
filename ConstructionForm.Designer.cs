using System.Windows.Forms;
using System;

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
            this.Text = "City Construction";
            this.Size = new System.Drawing.Size(400, 400);

            listBoxProjects = new ListBox();
            listBoxProjects.Dock = DockStyle.Top;
            listBoxProjects.Height = 180;

            labelType = new Label();
            labelType.Text = "Project Type:";
            labelType.Location = new System.Drawing.Point(10, 190);

            comboBoxType = new ComboBox();
            comboBoxType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxType.Items.AddRange(Enum.GetNames(typeof(ProjectType)));
            comboBoxType.SelectedIndex = 0;
            comboBoxType.Location = new System.Drawing.Point(120, 188);

            labelCost = new Label();
            labelCost.Text = "Cost:";
            labelCost.Location = new System.Drawing.Point(10, 220);

            numericCost = new NumericUpDown();
            numericCost.Minimum = 0;
            numericCost.Maximum = 1000000;
            numericCost.Value = 1000;
            numericCost.Location = new System.Drawing.Point(120, 218);

            labelDuration = new Label();
            labelDuration.Text = "Duration (days):";
            labelDuration.Location = new System.Drawing.Point(10, 250);

            numericDuration = new NumericUpDown();
            numericDuration.Minimum = 1;
            numericDuration.Maximum = 365;
            numericDuration.Value = 30;
            numericDuration.Location = new System.Drawing.Point(120, 248);

            labelOutput = new Label();
            labelOutput.Text = "Output:";
            labelOutput.Location = new System.Drawing.Point(10, 280);

            numericOutput = new NumericUpDown();
            numericOutput.Minimum = 1;
            numericOutput.Maximum = 1000;
            numericOutput.Value = 10;
            numericOutput.Location = new System.Drawing.Point(120, 278);

            buttonStart = new Button();
            buttonStart.Text = "Start Project";
            buttonStart.Location = new System.Drawing.Point(10, 320);
            buttonStart.Click += ButtonStart_Click;

            this.Controls.Add(listBoxProjects);
            this.Controls.Add(labelType);
            this.Controls.Add(comboBoxType);
            this.Controls.Add(labelCost);
            this.Controls.Add(numericCost);
            this.Controls.Add(labelDuration);
            this.Controls.Add(numericDuration);
            this.Controls.Add(labelOutput);
            this.Controls.Add(numericOutput);
            this.Controls.Add(buttonStart);
        }
    }
}
