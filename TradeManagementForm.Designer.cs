using System.Drawing;
using System.Windows.Forms;

namespace economy_sim
{
    public partial class TradeManagementForm : Form
    {
       
        private TabControl tabControlTrade;
        private TabPage tabPageTradeRoutes;
        private TabPage tabPageTradeAgreements;
        private TabPage tabPageGlobalMarket;

        private ListView listViewTradeRoutes;
        private Button buttonCreateRoute;
        private Button buttonUpgradeRoute;
        private Button buttonDeleteRoute;
        private ComboBox comboBoxRouteType;

        private ListView listViewTradeAgreements;
        private Button buttonNewAgreement;
        private Button buttonRenewAgreement;
        private Button buttonCancelAgreement;

        private ListView listViewGlobalPrices;
        private ListView listViewTradingPartners;
        private Label labelGlobalTradeValue;
        private ComboBox comboBoxCommodityFilter;

        private Panel panelCreateRoute;
        private ComboBox comboBoxStartCity;
        private ComboBox comboBoxEndCity;
        private ComboBox comboBoxNewRouteType;
        private Button buttonConfirmNewRoute;
        private Button buttonCancelNewRoute;

        private void InitializeComponent()
        {
            buttonRefresh = new Button();
            tabControlTrade = new TabControl();
            tabPageTradeRoutes = new TabPage("Trade Routes");
            tabPageTradeAgreements = new TabPage("Trade Agreements");
            tabPageGlobalMarket = new TabPage("Global Market");
            SuspendLayout();

            // buttonRefresh
            buttonRefresh.Text = "🔄 Refresh";
            buttonRefresh.Location = new Point(10, 530);
            buttonRefresh.Size = new Size(100, 30);
            buttonRefresh.Click += ButtonRefresh_Click;

            // tabControlTrade
            tabControlTrade.Dock = DockStyle.Fill;

            // Trade Routes Tab
            InitializeTradeRoutesTab();
            tabControlTrade.TabPages.Add(tabPageTradeRoutes);

            // Trade Agreements Tab
            InitializeTradeAgreementsTab();
            tabControlTrade.TabPages.Add(tabPageTradeAgreements);

            // Global Market Tab
            InitializeGlobalMarketTab();
            tabControlTrade.TabPages.Add(tabPageGlobalMarket);

            Controls.Add(tabControlTrade);
            Controls.Add(buttonRefresh);

            // Route creation panel
            InitializeRouteCreationPanel();
            tabPageTradeRoutes.Controls.Add(panelCreateRoute);
            panelCreateRoute.Visible = false;

            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 600);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Text = "Trade Management";
            ResumeLayout(false);
        }

        private void InitializeTradeRoutesTab()
        {
            listViewTradeRoutes = new ListView();
            listViewTradeRoutes.View = View.Details;
            listViewTradeRoutes.FullRowSelect = true;
            listViewTradeRoutes.GridLines = true;
            listViewTradeRoutes.Location = new Point(10, 10);
            listViewTradeRoutes.Size = new Size(765, 400);

            listViewTradeRoutes.Columns.Add("Route Name", 150);
            listViewTradeRoutes.Columns.Add("Type", 80);
            listViewTradeRoutes.Columns.Add("From", 100);
            listViewTradeRoutes.Columns.Add("To", 100);
            listViewTradeRoutes.Columns.Add("Distance", 70);
            listViewTradeRoutes.Columns.Add("Capacity", 70);
            listViewTradeRoutes.Columns.Add("Usage", 70);
            listViewTradeRoutes.Columns.Add("Status", 80);

            Label labelRouteFilter = new Label();
            labelRouteFilter.Text = "Filter by Type:";
            labelRouteFilter.Location = new Point(10, 420);
            labelRouteFilter.Size = new Size(100, 20);

            comboBoxRouteType = new ComboBox();
            comboBoxRouteType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxRouteType.Location = new Point(110, 420);
            comboBoxRouteType.Size = new Size(120, 20);
            comboBoxRouteType.Items.AddRange(new object[] { "All", "Land", "Sea", "River", "Air" });
            comboBoxRouteType.SelectedIndex = 0;
            comboBoxRouteType.SelectedIndexChanged += ComboBoxRouteType_SelectedIndexChanged;

            buttonCreateRoute = new Button();
            buttonCreateRoute.Text = "Create New Route";
            buttonCreateRoute.Location = new Point(10, 460);
            buttonCreateRoute.Size = new Size(120, 30);
            buttonCreateRoute.Click += ButtonCreateRoute_Click;

            buttonUpgradeRoute = new Button();
            buttonUpgradeRoute.Text = "Upgrade Route";
            buttonUpgradeRoute.Location = new Point(140, 460);
            buttonUpgradeRoute.Size = new Size(120, 30);
            buttonUpgradeRoute.Click += ButtonUpgradeRoute_Click;

            buttonDeleteRoute = new Button();
            buttonDeleteRoute.Text = "Delete Route";
            buttonDeleteRoute.Location = new Point(270, 460);
            buttonDeleteRoute.Size = new Size(120, 30);
            buttonDeleteRoute.Click += ButtonDeleteRoute_Click;

            tabPageTradeRoutes.Controls.Add(listViewTradeRoutes);
            tabPageTradeRoutes.Controls.Add(labelRouteFilter);
            tabPageTradeRoutes.Controls.Add(comboBoxRouteType);
            tabPageTradeRoutes.Controls.Add(buttonCreateRoute);
            tabPageTradeRoutes.Controls.Add(buttonUpgradeRoute);
            tabPageTradeRoutes.Controls.Add(buttonDeleteRoute);
        }

        private void InitializeTradeAgreementsTab()
        {
            listViewTradeAgreements = new ListView();
            listViewTradeAgreements.View = View.Details;
            listViewTradeAgreements.FullRowSelect = true;
            listViewTradeAgreements.GridLines = true;
            listViewTradeAgreements.Location = new Point(10, 10);
            listViewTradeAgreements.Size = new Size(765, 400);

            listViewTradeAgreements.Columns.Add("Agreement", 150);
            listViewTradeAgreements.Columns.Add("Type", 80);
            listViewTradeAgreements.Columns.Add("With", 100);
            listViewTradeAgreements.Columns.Add("Goods", 120);
            listViewTradeAgreements.Columns.Add("Tariff Rate", 80);
            listViewTradeAgreements.Columns.Add("Duration", 70);
            listViewTradeAgreements.Columns.Add("Status", 80);

            buttonNewAgreement = new Button();
            buttonNewAgreement.Text = "New Agreement";
            buttonNewAgreement.Location = new Point(10, 430);
            buttonNewAgreement.Size = new Size(120, 30);
            buttonNewAgreement.Click += ButtonNewAgreement_Click;

            buttonRenewAgreement = new Button();
            buttonRenewAgreement.Text = "Renew Agreement";
            buttonRenewAgreement.Location = new Point(140, 430);
            buttonRenewAgreement.Size = new Size(120, 30);
            buttonRenewAgreement.Click += ButtonRenewAgreement_Click;

            buttonCancelAgreement = new Button();
            buttonCancelAgreement.Text = "Cancel Agreement";
            buttonCancelAgreement.Location = new Point(270, 430);
            buttonCancelAgreement.Size = new Size(120, 30);
            buttonCancelAgreement.Click += ButtonCancelAgreement_Click;

            tabPageTradeAgreements.Controls.Add(listViewTradeAgreements);
            tabPageTradeAgreements.Controls.Add(buttonNewAgreement);
            tabPageTradeAgreements.Controls.Add(buttonRenewAgreement);
            tabPageTradeAgreements.Controls.Add(buttonCancelAgreement);
        }

        private void InitializeGlobalMarketTab()
        {
            Label playerCountryHeader = new Label();
            playerCountryHeader.Text = $"Managing Trade for: {(playerCountry != null ? playerCountry.Name : "N/A")}";
            playerCountryHeader.Location = new Point(10, 10);
            playerCountryHeader.Size = new Size(400, 20);
            playerCountryHeader.Font = new Font(playerCountryHeader.Font.FontFamily, 12, FontStyle.Bold);
            playerCountryHeader.ForeColor = Color.DarkGreen;

            labelGlobalTradeValue = new Label();
            labelGlobalTradeValue.Text = "Global Trade Value: $0";
            labelGlobalTradeValue.Location = new Point(410, 10);
            labelGlobalTradeValue.Size = new Size(300, 20);
            labelGlobalTradeValue.Font = new Font(labelGlobalTradeValue.Font, FontStyle.Bold);

            Label labelCommodityFilter = new Label();
            labelCommodityFilter.Text = "Select Commodity:";
            labelCommodityFilter.Location = new Point(10, 40);
            labelCommodityFilter.Size = new Size(120, 20);

            comboBoxCommodityFilter = new ComboBox();
            comboBoxCommodityFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCommodityFilter.Location = new Point(130, 40);
            comboBoxCommodityFilter.Size = new Size(150, 20);
            comboBoxCommodityFilter.SelectedIndexChanged += ComboBoxCommodityFilter_SelectedIndexChanged;

            Label labelPriceHeader = new Label();
            labelPriceHeader.Text = "Global Price Index:";
            labelPriceHeader.Location = new Point(10, 70);
            labelPriceHeader.Size = new Size(150, 20);

            listViewGlobalPrices = new ListView();
            listViewGlobalPrices.View = View.Details;
            listViewGlobalPrices.FullRowSelect = true;
            listViewGlobalPrices.GridLines = true;
            listViewGlobalPrices.Location = new Point(10, 100);
            listViewGlobalPrices.Size = new Size(380, 400);

            listViewGlobalPrices.Columns.Add("Commodity", 120);
            listViewGlobalPrices.Columns.Add("Price", 80);
            listViewGlobalPrices.Columns.Add("Change", 80);
            listViewGlobalPrices.Columns.Add("Volatility", 80);

            Label labelPartnersHeader = new Label();
            labelPartnersHeader.Text = "Top Trading Partners:";
            labelPartnersHeader.Location = new Point(400, 70);
            labelPartnersHeader.Size = new Size(150, 20);

            listViewTradingPartners = new ListView();
            listViewTradingPartners.View = View.Details;
            listViewTradingPartners.FullRowSelect = true;
            listViewTradingPartners.GridLines = true;
            listViewTradingPartners.Location = new Point(400, 100);
            listViewTradingPartners.Size = new Size(370, 400);

            listViewTradingPartners.Columns.Add("Country", 120);
            listViewTradingPartners.Columns.Add("Export Volume", 100);
            listViewTradingPartners.Columns.Add("Import Volume", 100);

            tabPageGlobalMarket.Controls.Add(labelGlobalTradeValue);
            tabPageGlobalMarket.Controls.Add(labelCommodityFilter);
            tabPageGlobalMarket.Controls.Add(comboBoxCommodityFilter);
            tabPageGlobalMarket.Controls.Add(labelPriceHeader);
            tabPageGlobalMarket.Controls.Add(listViewGlobalPrices);
            tabPageGlobalMarket.Controls.Add(labelPartnersHeader);
            tabPageGlobalMarket.Controls.Add(listViewTradingPartners);

            if (globalMarket != null && globalMarket.GlobalPrices != null)
            {
                comboBoxCommodityFilter.Items.Add("All Commodities");
                foreach (var goodName in globalMarket.GlobalPrices.Keys)
                {
                    comboBoxCommodityFilter.Items.Add(goodName);
                }
                comboBoxCommodityFilter.SelectedIndex = 0;
            }
        }

        private void InitializeRouteCreationPanel()
        {
            panelCreateRoute = new Panel();
            panelCreateRoute.BorderStyle = BorderStyle.FixedSingle;
            panelCreateRoute.BackColor = Color.AntiqueWhite;
            panelCreateRoute.Location = new Point(200, 100);
            panelCreateRoute.Size = new Size(400, 300);

            Label labelPanelTitle = new Label();
            labelPanelTitle.Text = "Create New Trade Route";
            labelPanelTitle.Location = new Point(10, 10);
            labelPanelTitle.Size = new Size(380, 25);
            labelPanelTitle.TextAlign = ContentAlignment.MiddleCenter;
            labelPanelTitle.Font = new Font(labelPanelTitle.Font, FontStyle.Bold);

            Label labelStartCity = new Label();
            labelStartCity.Text = "Start City:";
            labelStartCity.Location = new Point(10, 50);
            labelStartCity.Size = new Size(100, 20);

            comboBoxStartCity = new ComboBox();
            comboBoxStartCity.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxStartCity.Location = new Point(120, 50);
            comboBoxStartCity.Size = new Size(250, 20);

            Label labelEndCity = new Label();
            labelEndCity.Text = "End City:";
            labelEndCity.Location = new Point(10, 90);
            labelEndCity.Size = new Size(100, 20);

            comboBoxEndCity = new ComboBox();
            comboBoxEndCity.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxEndCity.Location = new Point(120, 90);
            comboBoxEndCity.Size = new Size(250, 20);

            Label labelRouteType = new Label();
            labelRouteType.Text = "Route Type:";
            labelRouteType.Location = new Point(10, 130);
            labelRouteType.Size = new Size(100, 20);

            comboBoxNewRouteType = new ComboBox();
            comboBoxNewRouteType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxNewRouteType.Location = new Point(120, 130);
            comboBoxNewRouteType.Size = new Size(250, 20);
            comboBoxNewRouteType.Items.AddRange(new object[] { "Land", "Sea", "River", "Air" });
            comboBoxNewRouteType.SelectedIndex = 0;

            Label labelInfo = new Label();
            labelInfo.Text = "Note: Route costs will vary based on type and distance.";
            labelInfo.Location = new Point(10, 170);
            labelInfo.Size = new Size(380, 40);

            buttonConfirmNewRoute = new Button();
            buttonConfirmNewRoute.Text = "Create";
            buttonConfirmNewRoute.Location = new Point(100, 230);
            buttonConfirmNewRoute.Size = new Size(80, 30);
            buttonConfirmNewRoute.Click += ButtonConfirmNewRoute_Click;

            buttonCancelNewRoute = new Button();
            buttonCancelNewRoute.Text = "Cancel";
            buttonCancelNewRoute.Location = new Point(220, 230);
            buttonCancelNewRoute.Size = new Size(80, 30);
            buttonCancelNewRoute.Click += ButtonCancelNewRoute_Click;

            panelCreateRoute.Controls.Add(labelPanelTitle);
            panelCreateRoute.Controls.Add(labelStartCity);
            panelCreateRoute.Controls.Add(comboBoxStartCity);
            panelCreateRoute.Controls.Add(labelEndCity);
            panelCreateRoute.Controls.Add(comboBoxEndCity);
            panelCreateRoute.Controls.Add(labelRouteType);
            panelCreateRoute.Controls.Add(comboBoxNewRouteType);
            panelCreateRoute.Controls.Add(labelInfo);
            panelCreateRoute.Controls.Add(buttonConfirmNewRoute);
            panelCreateRoute.Controls.Add(buttonCancelNewRoute);

            if (allCities != null)
            {
                foreach (var city in allCities.Where(c => PlayerControlsCity(c)))
                {
                    comboBoxStartCity.Items.Add(city.Name);
                }

                foreach (var city in allCities)
                {
                    comboBoxEndCity.Items.Add(city.Name);
                }

                if (comboBoxStartCity.Items.Count > 0)
                    comboBoxStartCity.SelectedIndex = 0;

                if (comboBoxEndCity.Items.Count > 0)
                    comboBoxEndCity.SelectedIndex = 0;
            }
        }
    }
}
