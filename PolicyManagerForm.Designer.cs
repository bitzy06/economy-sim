using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class PolicyManagerForm : Form
    {
        private ListView listViewPolicies;
        private ListView listViewDepartments;

        private void InitializeComponent()
        {
            this.listViewPolicies = new System.Windows.Forms.ListView();
            this.listViewDepartments = new System.Windows.Forms.ListView();
            this.SuspendLayout();
            // 
            // listViewPolicies
            // 
            this.listViewPolicies.View = System.Windows.Forms.View.Details;
            this.listViewPolicies.FullRowSelect = true;
            this.listViewPolicies.GridLines = true;
            this.listViewPolicies.Location = new System.Drawing.Point(10, 10);
            this.listViewPolicies.Size = new System.Drawing.Size(560, 200);
            this.listViewPolicies.Columns.Add("Policy", 200);
            this.listViewPolicies.Columns.Add("Type", 100);
            this.listViewPolicies.Columns.Add("Active", 80);
            // 
            // listViewDepartments
            // 
            this.listViewDepartments.View = System.Windows.Forms.View.Details;
            this.listViewDepartments.FullRowSelect = true;
            this.listViewDepartments.GridLines = true;
            this.listViewDepartments.Location = new System.Drawing.Point(10, 220);
            this.listViewDepartments.Size = new System.Drawing.Size(560, 120);
            this.listViewDepartments.Columns.Add("Department", 200);
            this.listViewDepartments.Columns.Add("Budget", 100);
            // 
            // PolicyManagerForm
            // 
            this.ClientSize = new System.Drawing.Size(600, 400);
            this.Controls.Add(this.listViewPolicies);
            this.Controls.Add(this.listViewDepartments);
            this.Name = "PolicyManagerForm";
            this.Text = "Policy Manager";
            this.ResumeLayout(false);
        }
    }
}
