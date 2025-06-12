using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using StrategyGame; // Assuming City, Factory, Good classes are in this namespace

namespace economy_sim // Assuming this is your project's namespace
{
    public partial class FactoryStatsForm : Form
    {
        private ListBox listBoxFactoryDetails;

        public FactoryStatsForm()
        {
            InitializeComponent(); // This will be auto-generated in FactoryStatsForm.Designer.cs
            // Manual setup for the ListBox if not using the designer for this part:
            this.listBoxFactoryDetails = new System.Windows.Forms.ListBox();
            this.listBoxFactoryDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBoxFactoryDetails.FormattingEnabled = true;
            this.listBoxFactoryDetails.ItemHeight = 15; // Default, can be adjusted
            this.listBoxFactoryDetails.IntegralHeight = false; // Allows partial last item if needed
            this.Controls.Add(this.listBoxFactoryDetails);
            this.Text = "Factory Details";
            this.Size = new System.Drawing.Size(600, 400); // Initial size
        }

        public void UpdateStats(City city)
        {
            int previousTopIndex = 0;
            if (listBoxFactoryDetails.Items.Count > 0)
            {
                previousTopIndex = listBoxFactoryDetails.TopIndex;
            }

            listBoxFactoryDetails.BeginUpdate(); // Prevent flickering and improve performance
            listBoxFactoryDetails.Items.Clear();
            if (city == null)
            {
                listBoxFactoryDetails.Items.Add("No city selected.");
                return;
            }

            listBoxFactoryDetails.Items.Add($"--- Factories in {city.Name} ---");
            if (!city.Factories.Any())
            {
                listBoxFactoryDetails.Items.Add("No factories in this city.");
                return;
            }

            foreach (var factory in city.Factories)
            {
                listBoxFactoryDetails.Items.Add($""); // Blank line for spacing
                listBoxFactoryDetails.Items.Add($"Factory: {factory.Name}");
                listBoxFactoryDetails.Items.Add($"  Capacity: {factory.ProductionCapacity}");
                listBoxFactoryDetails.Items.Add($"  Total Employed: {factory.WorkersEmployed}");

                // Job Slots and Actual Employed
                if (factory.JobSlots.Any())
                {
                    listBoxFactoryDetails.Items.Add($"  Job Slots (Available/Filled):");
                    foreach (var slot in factory.JobSlots)
                    {
                        int actual = factory.ActualEmployed.ContainsKey(slot.Key) ? factory.ActualEmployed[slot.Key] : 0;
                        listBoxFactoryDetails.Items.Add($"    - {slot.Key}: {slot.Value}/{actual}");
                    }
                }
                else
                {
                    listBoxFactoryDetails.Items.Add($"  Job Slots: (None defined)");
                }
                
                // Input Goods
                if (factory.InputGoods.Any())
                {
                    listBoxFactoryDetails.Items.Add($"  Inputs (per unit of capacity per cycle):");
                    foreach (var input in factory.InputGoods)
                    {
                        listBoxFactoryDetails.Items.Add($"    - {input.Name}: {input.Quantity}");
                    }
                }
                else
                {
                    listBoxFactoryDetails.Items.Add($"  Inputs: (None)");
                }

                // Output Goods
                if (factory.OutputGoods.Any())
                {
                    listBoxFactoryDetails.Items.Add($"  Outputs (per unit of capacity per cycle):");
                    foreach (var output in factory.OutputGoods)
                    {
                        listBoxFactoryDetails.Items.Add($"    - {output.Name}: {output.Quantity}");
                    }
                }
                else
                {
                    listBoxFactoryDetails.Items.Add($"  Outputs: (None)");
                }
            }
            listBoxFactoryDetails.EndUpdate(); // Re-enable drawing

            if (previousTopIndex >= 0 && previousTopIndex < listBoxFactoryDetails.Items.Count)
            {
                listBoxFactoryDetails.TopIndex = previousTopIndex;
            }
            else if (listBoxFactoryDetails.Items.Count > 0)
            {
                listBoxFactoryDetails.TopIndex = 0; // Default to top if previous index is invalid
            }
        }

        // Minimal InitializeComponent - actual designer code would be in FactoryStatsForm.Designer.cs
        // This is just to make the .cs file compilable standalone for this step.
        // In a real VS project, you'd let the designer handle this.
        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // FactoryStatsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 361); // Example
            this.Name = "FactoryStatsForm";
            this.Text = "Factory Details";
            this.ResumeLayout(false);
        }
    }
} 