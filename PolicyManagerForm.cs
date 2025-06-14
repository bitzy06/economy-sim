using System.Windows.Forms;

namespace economy_sim
{
    public partial class PolicyManagerForm : Form
    {
        private StrategyGame.Government government;

        public PolicyManagerForm(StrategyGame.Government gov)
        {
            government = gov;
            InitializeComponent();
        }

        public void RefreshData()
        {
            if (government == null) return;
            listViewPolicies.Items.Clear();
            foreach (var policy in government.Policies)
            {
                var item = new ListViewItem(policy.Name);
                item.SubItems.Add(policy.Type.ToString());
                item.SubItems.Add(policy.IsActive ? "Yes" : "No");
                listViewPolicies.Items.Add(item);
            }

            listViewDepartments.Items.Clear();
            foreach (var dept in government.Departments)
            {
                var item = new ListViewItem(dept.Name);
                item.SubItems.Add(dept.BudgetAllocation.ToString("N0"));
                listViewDepartments.Items.Add(item);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            e.Cancel = true;
            this.Hide();
        }
    }
}
