using System;
using System.Linq;
using System.Windows.Forms;
using StrategyGame;

namespace economy_sim
{
    public class ConstructionForm : Form
    {
        private ListBox listBoxProjects;
        private ComboBox comboBoxType;
        private NumericUpDown numericCost;
        private NumericUpDown numericDuration;
        private NumericUpDown numericOutput;
        private Button buttonStart;
        private City currentCity;

        public ConstructionForm()
        {
            this.Text = "City Construction";
            this.Size = new System.Drawing.Size(400, 400);

            listBoxProjects = new ListBox();
            listBoxProjects.Dock = DockStyle.Top;
            listBoxProjects.Height = 180;

            comboBoxType = new ComboBox();
            comboBoxType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxType.Items.AddRange(Enum.GetNames(typeof(ProjectType)));
            comboBoxType.SelectedIndex = 0;
            comboBoxType.Location = new System.Drawing.Point(10, 200);

            numericCost = new NumericUpDown();
            numericCost.Minimum = 0;
            numericCost.Maximum = 1000000;
            numericCost.Value = 1000;
            numericCost.Location = new System.Drawing.Point(10, 230);

            numericDuration = new NumericUpDown();
            numericDuration.Minimum = 1;
            numericDuration.Maximum = 365;
            numericDuration.Value = 30;
            numericDuration.Location = new System.Drawing.Point(10, 260);

            numericOutput = new NumericUpDown();
            numericOutput.Minimum = 1;
            numericOutput.Maximum = 1000;
            numericOutput.Value = 10;
            numericOutput.Location = new System.Drawing.Point(10, 290);

            buttonStart = new Button();
            buttonStart.Text = "Start Project";
            buttonStart.Location = new System.Drawing.Point(10, 320);
            buttonStart.Click += ButtonStart_Click;

            this.Controls.Add(listBoxProjects);
            this.Controls.Add(comboBoxType);
            this.Controls.Add(numericCost);
            this.Controls.Add(numericDuration);
            this.Controls.Add(numericOutput);
            this.Controls.Add(buttonStart);
        }

        public void SetCity(City city)
        {
            currentCity = city;
        }

        public void UpdateProjects()
        {
            listBoxProjects.Items.Clear();
            if (currentCity == null)
            {
                listBoxProjects.Items.Add("No city selected.");
                return;
            }

            listBoxProjects.Items.Add($"Active Projects in {currentCity.Name}");
            if (!currentCity.ActiveProjects.Any())
            {
                listBoxProjects.Items.Add("(none)");
                return;
            }
            foreach (var p in currentCity.ActiveProjects)
            {
                listBoxProjects.Items.Add($"{p.Type} - {p.Progress}/{p.Duration} days, Cost {p.Cost}, Output {p.Output}");
            }
        }

        private void ButtonStart_Click(object sender, EventArgs e)
        {
            if (currentCity == null) return;
            var type = (ProjectType)Enum.Parse(typeof(ProjectType), comboBoxType.SelectedItem.ToString());
            decimal cost = numericCost.Value;
            int dur = (int)numericDuration.Value;
            double output = (double)numericOutput.Value;
            var proj = new ConstructionProject(type, cost, dur, output);
            currentCity.StartConstructionProject(proj);
            UpdateProjects();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}

