using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class PopStatsForm : Form
    {
        public PopStatsForm()
        {
            InitializeComponent();
        }

        public void UpdateStats(StrategyGame.City city)
        {
            listBoxPopStats.Items.Clear();
            listBoxPopStats.Items.Add($"Pop Class Stats for {city.Name}");
            listBoxPopStats.Items.Add($"City QoL: {city.CalculateCityQualityOfLife():0.00}"); // Added city QoL

            foreach (var pop in city.PopClasses)
            {
                listBoxPopStats.Items.Add($"{pop.Name}: Size={pop.Size}, Employed={pop.Employed}, Unemployed={pop.Unemployed}, Income/Person={pop.IncomePerPerson:0.00}");
                string needs = string.Join(", ", pop.Needs.Select(n => $"{n.Key}:{n.Value}"));
                listBoxPopStats.Items.Add($"  Needs: {needs}");
                listBoxPopStats.Items.Add($"  Unmet Needs: {pop.UnmetNeeds}, Happiness: {pop.Happiness}");
            }

            foreach (var suburb in city.Suburbs)
            {
                listBoxPopStats.Items.Add($"Suburb: {suburb.Name}, QoL: {suburb.CalculateQualityOfLife():0.00}, Housing Capacity: {suburb.HousingCapacity:0.00}, Railway: {suburb.RailwayKilometers:0.00} km");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}