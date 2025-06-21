using System;
using System.Linq;
using System.Windows.Forms;
using StrategyGame;

namespace economy_sim
{
    public partial class ConstructionForm : Form
    {
        private City currentCity;
        public City CurrentCity => currentCity;

        public ConstructionForm()
        {
            InitializeComponent();
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
            
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}

