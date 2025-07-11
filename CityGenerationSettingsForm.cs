using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class CityGenerationSettingsForm : Form
    {
        public List<string> SelectedCountries { get; private set; } = new();

        public CityGenerationSettingsForm(IEnumerable<string> countries)
        {
            InitializeComponent();
            foreach (var c in countries)
            {
                checkedListBoxCountries.Items.Add(c, true);
            }
        }

        private void ButtonGenerate_Click(object sender, EventArgs e)
        {
            SelectedCountries = checkedListBoxCountries.CheckedItems.Cast<string>().ToList();
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
