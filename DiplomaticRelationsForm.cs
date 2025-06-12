using System;
using System.Windows.Forms;
using System.Drawing;
using System.Linq;

namespace economy_sim
{
    public partial class DiplomaticRelationsForm : Form
    {
        private ListView listViewRelations;
        private StrategyGame.Country playerCountry;
        private System.Collections.Generic.List<StrategyGame.Country> allCountries;
        private StrategyGame.DiplomacyManager diplomacyManager;

        public DiplomaticRelationsForm(StrategyGame.Country player, System.Collections.Generic.List<StrategyGame.Country> countries, StrategyGame.DiplomacyManager dipManager)
        {
            playerCountry = player;
            allCountries = countries;
            diplomacyManager = dipManager;
            InitializeComponent();
            PopulateRelations();
        }

        private void InitializeComponent()
        {
            this.Text = "Diplomatic Relations";
            this.Size = new System.Drawing.Size(600, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            listViewRelations = new ListView
            {
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(565, 340),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            listViewRelations.Columns.Add("Country", 120);
            listViewRelations.Columns.Add("Current Stance", 120);
            listViewRelations.Columns.Add("Active Trades", 200);
            listViewRelations.Columns.Add("Duration", 100);

            Controls.Add(listViewRelations);
        }

        private void PopulateRelations()
        {
            listViewRelations.Items.Clear();

            foreach (var country in allCountries.Where(c => c != playerCountry))
            {
                var relation = diplomacyManager.GetRelation(playerCountry.Name, country.Name);
                var trades = diplomacyManager.GetTradeAgreementsForCountry(country.Name)
                    .Where(t => t.Status == StrategyGame.TradeStatus.Active &&
                           (t.FromCountryName == playerCountry.Name || t.ToCountryName == playerCountry.Name));

                var item = new ListViewItem(country.Name);
                
                // Add diplomatic stance
                string stance = relation != null ? relation.Stance.ToString() : "Peace";
                item.SubItems.Add(stance);

                // Add active trades summary
                string tradesSummary = string.Join(", ", trades.Select(t => 
                    $"{(t.FromCountryName == playerCountry.Name ? "→" : "←")} {t.Quantity} {t.ResourceName}"));
                item.SubItems.Add(tradesSummary);

                // Add duration if applicable
                string duration = relation != null && relation.TurnsRemaining > 0 
                    ? $"{relation.TurnsRemaining} turns" 
                    : "Permanent";
                item.SubItems.Add(duration);

                // Color coding based on stance
                if (relation != null)
                {
                    switch (relation.Stance)
                    {
                        case StrategyGame.DiplomaticStance.War:
                            item.BackColor = Color.LightPink;
                            break;
                        case StrategyGame.DiplomaticStance.Alliance:
                            item.BackColor = Color.LightGreen;
                            break;
                        case StrategyGame.DiplomaticStance.TradeEmbargo:
                            item.BackColor = Color.LightYellow;
                            break;
                    }
                }

                listViewRelations.Items.Add(item);
            }
        }
    }
}
