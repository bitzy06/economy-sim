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
        private NumericUpDown numericBudget;
        private NumericUpDown numericDuration;
        private NumericUpDown numericOutput;
        private Label labelType;
        private Label labelBudget;
        private Label labelDuration;
        private Label labelOutput;
        private Button buttonStart;
        private City currentCity;
        public City CurrentCity => currentCity;

        public ConstructionForm()
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

            labelBudget = new Label();
            labelBudget.Text = "Budget:";
            labelBudget.Location = new System.Drawing.Point(10, 220);

            numericBudget = new NumericUpDown();
            numericBudget.Minimum = 0;
            numericBudget.Maximum = 1000000;
            numericBudget.Value = 1000;
            numericBudget.Location = new System.Drawing.Point(120, 218);

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
            this.Controls.Add(labelBudget);
            this.Controls.Add(numericBudget);
            this.Controls.Add(labelDuration);
            this.Controls.Add(numericDuration);
            this.Controls.Add(labelOutput);
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
                listBoxProjects.Items.Add($"{p.Type} - {p.Progress}/{p.Duration} days, Budget Left {p.BudgetRemaining}, Output {p.Output}");
            }
        }

        private void ButtonStart_Click(object sender, EventArgs e)
        {
            if (currentCity == null) return;
            var type = (ProjectType)Enum.Parse(typeof(ProjectType), comboBoxType.SelectedItem.ToString());
            decimal budget = numericBudget.Value;
            int dur = (int)numericDuration.Value;
            double output = (double)numericOutput.Value;
            decimal minBudget = ConstructionProject.MinimumDailyBudget * dur;
            if (budget < minBudget)
            {
                MessageBox.Show($"Budget must be at least {minBudget}", "Invalid Budget", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string resource = type == ProjectType.Housing ? "Timber" : "Steel";
            int resPerDay = type == ProjectType.Housing ? 5 : 3;

            var proj = new ConstructionProject(type, budget, dur, output, resource, resPerDay);
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

