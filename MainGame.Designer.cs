namespace economy_sim
{
    partial class MainGame
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Timer timerSim;
        private System.Windows.Forms.Label labelSimTime;
        private System.Windows.Forms.ListBox listBoxMarketStats;
        private System.Windows.Forms.TabPage tabPageCompanies;
        private System.Windows.Forms.ListView listViewCompanies;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            timerSim = new System.Windows.Forms.Timer(components);
            labelSimTime = new Label();
            listBoxMarketStats = new ListBox();
            tabPageCompanies = new TabPage();
            listViewCompanies = new ListView();
            tabPageFinance = new TabPage();
            listViewFinance = new ListView();
            tabPageDebug = new TabPage();
            buttonToggleDebug = new Button();
            labelCurrentRole = new Label();
            labelRoleType = new Label();
            comboBoxRoleType = new ComboBox();
            labelEntitySelection = new Label();
            comboBoxCountrySelection = new ComboBox();
            comboBoxStateSelection = new ComboBox();
            comboBoxCorporationSelection = new ComboBox();
            buttonAssumeRole = new Button();
            buttonRelinquishRole = new Button();
            checkBoxLogPops = new CheckBox();
            checkBoxLogBuildings = new CheckBox();
            checkBoxLogEconomy = new CheckBox();
            buttonGenerateTileCache = new Button();
            tabPageDiplomacy = new TabPage();
            labelProposedTrades = new Label();
            listBoxProposedTradeAgreements = new ListBox();
            buttonAcceptTrade = new Button();
            buttonRejectTrade = new Button();
            buttonProposeTrade = new Button();
            buttonViewRelations = new Button();
            buttonOpenTradeManagement = new Button();
            tabPageCity = new TabPage();
            comboBoxCountry = new ComboBox();
            labelCountryStats = new Label();
            comboBoxStates = new ComboBox();
            labelStateStats = new Label();
            comboBoxCities = new ComboBox();
            listBoxBuyOrders = new ListBox();
            listBoxSellOrders = new ListBox();
            listBoxCityStats = new ListBox();
            listBoxFactoryStats = new ListBox();
            tabPageCountry = new TabPage();
            panelMap = new Panel();
            pictureBox1 = new PictureBox();
            tabControlMain = new TabControl();
            tabPageCompanies.SuspendLayout();
            tabPageFinance.SuspendLayout();
            tabPageDebug.SuspendLayout();
            tabPageDiplomacy.SuspendLayout();
            tabPageCity.SuspendLayout();
            tabPageCountry.SuspendLayout();
            panelMap.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            tabControlMain.SuspendLayout();
            SuspendLayout();
            // 
            // timerSim
            // 
            timerSim.Interval = 1000;
            // 
            // labelSimTime
            // 
            labelSimTime.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            labelSimTime.Location = new Point(12, 577);
            labelSimTime.Margin = new Padding(4, 0, 4, 0);
            labelSimTime.Name = "labelSimTime";
            labelSimTime.Size = new Size(140, 27);
            labelSimTime.TabIndex = 1;
            labelSimTime.Text = "Turn: 0";
            // 
            // listBoxMarketStats
            // 
            listBoxMarketStats.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            listBoxMarketStats.FormattingEnabled = true;
            listBoxMarketStats.ItemHeight = 15;
            listBoxMarketStats.Location = new Point(175, 577);
            listBoxMarketStats.Margin = new Padding(4, 3, 4, 3);
            listBoxMarketStats.Name = "listBoxMarketStats";
            listBoxMarketStats.Size = new Size(804, 34);
            listBoxMarketStats.TabIndex = 2;
            // 
            // tabPageCompanies
            // 
            tabPageCompanies.Controls.Add(listViewCompanies);
            tabPageCompanies.Location = new Point(4, 22);
            tabPageCompanies.Name = "tabPageCompanies";
            tabPageCompanies.Size = new Size(822, 454);
            tabPageCompanies.TabIndex = 6;
            tabPageCompanies.Text = "Companies";
            tabPageCompanies.UseVisualStyleBackColor = true;
            // 
            // listViewCompanies
            // 
            listViewCompanies.FullRowSelect = true;
            listViewCompanies.GridLines = true;
            listViewCompanies.Location = new Point(10, 10);
            listViewCompanies.Name = "listViewCompanies";
            listViewCompanies.Size = new Size(800, 430);
            listViewCompanies.TabIndex = 0;
            listViewCompanies.UseCompatibleStateImageBehavior = false;
            listViewCompanies.View = View.Details;
            // 
            // tabPageFinance
            // 
            tabPageFinance.Controls.Add(listViewFinance);
            tabPageFinance.Location = new Point(4, 24);
            tabPageFinance.Margin = new Padding(4, 3, 4, 3);
            tabPageFinance.Name = "tabPageFinance";
            tabPageFinance.Size = new Size(960, 526);
            tabPageFinance.TabIndex = 5;
            tabPageFinance.Text = "Finance";
            tabPageFinance.UseVisualStyleBackColor = true;
            // 
            // listViewFinance
            // 
            listViewFinance.FullRowSelect = true;
            listViewFinance.GridLines = true;
            listViewFinance.Location = new Point(12, 12);
            listViewFinance.Margin = new Padding(4, 3, 4, 3);
            listViewFinance.Name = "listViewFinance";
            listViewFinance.Size = new Size(933, 496);
            listViewFinance.TabIndex = 0;
            listViewFinance.UseCompatibleStateImageBehavior = false;
            listViewFinance.View = View.Details;
            // 
            // tabPageDebug
            // 
            tabPageDebug.Controls.Add(buttonToggleDebug);
            tabPageDebug.Controls.Add(labelCurrentRole);
            tabPageDebug.Controls.Add(labelRoleType);
            tabPageDebug.Controls.Add(comboBoxRoleType);
            tabPageDebug.Controls.Add(labelEntitySelection);
            tabPageDebug.Controls.Add(comboBoxCountrySelection);
            tabPageDebug.Controls.Add(comboBoxStateSelection);
            tabPageDebug.Controls.Add(comboBoxCorporationSelection);
            tabPageDebug.Controls.Add(buttonAssumeRole);
            tabPageDebug.Controls.Add(buttonRelinquishRole);
            tabPageDebug.Controls.Add(checkBoxLogPops);
            tabPageDebug.Controls.Add(checkBoxLogBuildings);
            tabPageDebug.Controls.Add(checkBoxLogEconomy);
            tabPageDebug.Controls.Add(buttonGenerateTileCache);
            tabPageDebug.Location = new Point(4, 24);
            tabPageDebug.Margin = new Padding(4, 3, 4, 3);
            tabPageDebug.Name = "tabPageDebug";
            tabPageDebug.Padding = new Padding(4, 3, 4, 3);
            tabPageDebug.Size = new Size(960, 526);
            tabPageDebug.TabIndex = 4;
            tabPageDebug.Text = "Debug";
            tabPageDebug.UseVisualStyleBackColor = true;
            // 
            // buttonToggleDebug
            // 
            buttonToggleDebug.Location = new Point(12, 12);
            buttonToggleDebug.Margin = new Padding(4, 3, 4, 3);
            buttonToggleDebug.Name = "buttonToggleDebug";
            buttonToggleDebug.Size = new Size(140, 27);
            buttonToggleDebug.TabIndex = 0;
            buttonToggleDebug.Text = "Toggle Debug";
            buttonToggleDebug.UseVisualStyleBackColor = true;
            buttonToggleDebug.Click += ButtonToggleDebug_Click;
            // 
            // labelCurrentRole
            // 
            labelCurrentRole.AutoSize = true;
            labelCurrentRole.Location = new Point(12, 58);
            labelCurrentRole.Margin = new Padding(4, 0, 4, 0);
            labelCurrentRole.Name = "labelCurrentRole";
            labelCurrentRole.Size = new Size(76, 15);
            labelCurrentRole.TabIndex = 1;
            labelCurrentRole.Text = "Current Role:";
            // 
            // labelRoleType
            // 
            labelRoleType.AutoSize = true;
            labelRoleType.Location = new Point(12, 92);
            labelRoleType.Margin = new Padding(4, 0, 4, 0);
            labelRoleType.Name = "labelRoleType";
            labelRoleType.Size = new Size(61, 15);
            labelRoleType.TabIndex = 2;
            labelRoleType.Text = "Role Type:";
            // 
            // comboBoxRoleType
            // 
            comboBoxRoleType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxRoleType.FormattingEnabled = true;
            comboBoxRoleType.Location = new Point(93, 92);
            comboBoxRoleType.Margin = new Padding(4, 3, 4, 3);
            comboBoxRoleType.Name = "comboBoxRoleType";
            comboBoxRoleType.Size = new Size(233, 23);
            comboBoxRoleType.TabIndex = 3;
            // 
            // labelEntitySelection
            // 
            labelEntitySelection.AutoSize = true;
            labelEntitySelection.Location = new Point(12, 127);
            labelEntitySelection.Margin = new Padding(4, 0, 4, 0);
            labelEntitySelection.Name = "labelEntitySelection";
            labelEntitySelection.Size = new Size(91, 15);
            labelEntitySelection.TabIndex = 4;
            labelEntitySelection.Text = "Entity Selection:";
            // 
            // comboBoxCountrySelection
            // 
            comboBoxCountrySelection.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCountrySelection.FormattingEnabled = true;
            comboBoxCountrySelection.Location = new Point(12, 150);
            comboBoxCountrySelection.Margin = new Padding(4, 3, 4, 3);
            comboBoxCountrySelection.Name = "comboBoxCountrySelection";
            comboBoxCountrySelection.Size = new Size(233, 23);
            comboBoxCountrySelection.TabIndex = 5;
            // 
            // comboBoxStateSelection
            // 
            comboBoxStateSelection.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxStateSelection.FormattingEnabled = true;
            comboBoxStateSelection.Location = new Point(12, 185);
            comboBoxStateSelection.Margin = new Padding(4, 3, 4, 3);
            comboBoxStateSelection.Name = "comboBoxStateSelection";
            comboBoxStateSelection.Size = new Size(233, 23);
            comboBoxStateSelection.TabIndex = 6;
            // 
            // comboBoxCorporationSelection
            // 
            comboBoxCorporationSelection.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCorporationSelection.FormattingEnabled = true;
            comboBoxCorporationSelection.Location = new Point(12, 219);
            comboBoxCorporationSelection.Margin = new Padding(4, 3, 4, 3);
            comboBoxCorporationSelection.Name = "comboBoxCorporationSelection";
            comboBoxCorporationSelection.Size = new Size(233, 23);
            comboBoxCorporationSelection.TabIndex = 7;
            // 
            // buttonAssumeRole
            // 
            buttonAssumeRole.Location = new Point(12, 254);
            buttonAssumeRole.Margin = new Padding(4, 3, 4, 3);
            buttonAssumeRole.Name = "buttonAssumeRole";
            buttonAssumeRole.Size = new Size(140, 27);
            buttonAssumeRole.TabIndex = 8;
            buttonAssumeRole.Text = "Assume Role";
            buttonAssumeRole.UseVisualStyleBackColor = true;
            buttonAssumeRole.Click += ButtonAssumeRole_Click;
            // 
            // buttonRelinquishRole
            // 
            buttonRelinquishRole.Location = new Point(12, 288);
            buttonRelinquishRole.Margin = new Padding(4, 3, 4, 3);
            buttonRelinquishRole.Name = "buttonRelinquishRole";
            buttonRelinquishRole.Size = new Size(140, 27);
            buttonRelinquishRole.TabIndex = 9;
            buttonRelinquishRole.Text = "Relinquish Role";
            buttonRelinquishRole.UseVisualStyleBackColor = true;
            buttonRelinquishRole.Click += ButtonRelinquishRole_Click;
            // 
            // checkBoxLogPops
            // 
            checkBoxLogPops.AutoSize = true;
            checkBoxLogPops.Location = new Point(175, 323);
            checkBoxLogPops.Margin = new Padding(4, 3, 4, 3);
            checkBoxLogPops.Name = "checkBoxLogPops";
            checkBoxLogPops.Size = new Size(98, 19);
            checkBoxLogPops.TabIndex = 10;
            checkBoxLogPops.Text = "Log Pop Stats";
            checkBoxLogPops.UseVisualStyleBackColor = true;
            checkBoxLogPops.CheckedChanged += CheckBoxLogPops_CheckedChanged;
            // 
            // checkBoxLogBuildings
            // 
            checkBoxLogBuildings.AutoSize = true;
            checkBoxLogBuildings.Location = new Point(175, 346);
            checkBoxLogBuildings.Margin = new Padding(4, 3, 4, 3);
            checkBoxLogBuildings.Name = "checkBoxLogBuildings";
            checkBoxLogBuildings.Size = new Size(121, 19);
            checkBoxLogBuildings.TabIndex = 11;
            checkBoxLogBuildings.Text = "Log Building Stats";
            checkBoxLogBuildings.UseVisualStyleBackColor = true;
            checkBoxLogBuildings.CheckedChanged += CheckBoxLogBuildings_CheckedChanged;
            // 
            // checkBoxLogEconomy
            // 
            checkBoxLogEconomy.AutoSize = true;
            checkBoxLogEconomy.Location = new Point(175, 369);
            checkBoxLogEconomy.Margin = new Padding(4, 3, 4, 3);
            checkBoxLogEconomy.Name = "checkBoxLogEconomy";
            checkBoxLogEconomy.Size = new Size(127, 19);
            checkBoxLogEconomy.TabIndex = 12;
            checkBoxLogEconomy.Text = "Log Economy Stats";
            checkBoxLogEconomy.UseVisualStyleBackColor = true;
            checkBoxLogEconomy.CheckedChanged += CheckBoxLogEconomy_CheckedChanged;
            //
            // buttonGenerateTileCache
            //
            buttonGenerateTileCache.Location = new Point(12, 323);
            buttonGenerateTileCache.Margin = new Padding(4, 3, 4, 3);
            buttonGenerateTileCache.Name = "buttonGenerateTileCache";
            buttonGenerateTileCache.Size = new Size(140, 27);
            buttonGenerateTileCache.TabIndex = 13;
            buttonGenerateTileCache.Text = "Build Tile Cache";
            buttonGenerateTileCache.UseVisualStyleBackColor = true;
            buttonGenerateTileCache.Click += ButtonGenerateTileCache_Click;
            //
            // tabPageDiplomacy
            //
            tabPageDiplomacy.Controls.Add(labelProposedTrades);
            tabPageDiplomacy.Controls.Add(listBoxProposedTradeAgreements);
            tabPageDiplomacy.Controls.Add(buttonAcceptTrade);
            tabPageDiplomacy.Controls.Add(buttonRejectTrade);
            tabPageDiplomacy.Controls.Add(buttonProposeTrade);
            tabPageDiplomacy.Controls.Add(buttonViewRelations);
            tabPageDiplomacy.Controls.Add(buttonOpenTradeManagement);
            tabPageDiplomacy.Location = new Point(4, 24);
            tabPageDiplomacy.Margin = new Padding(4, 3, 4, 3);
            tabPageDiplomacy.Name = "tabPageDiplomacy";
            tabPageDiplomacy.Padding = new Padding(4, 3, 4, 3);
            tabPageDiplomacy.Size = new Size(960, 526);
            tabPageDiplomacy.TabIndex = 3;
            tabPageDiplomacy.Text = "Diplomacy";
            tabPageDiplomacy.UseVisualStyleBackColor = true;
            // 
            // labelProposedTrades
            // 
            labelProposedTrades.AutoSize = true;
            labelProposedTrades.Location = new Point(12, 219);
            labelProposedTrades.Margin = new Padding(4, 0, 4, 0);
            labelProposedTrades.Name = "labelProposedTrades";
            labelProposedTrades.Size = new Size(204, 15);
            labelProposedTrades.TabIndex = 0;
            labelProposedTrades.Text = "Proposed Trade Agreements (to you):";
            // 
            // listBoxProposedTradeAgreements
            // 
            listBoxProposedTradeAgreements.FormattingEnabled = true;
            listBoxProposedTradeAgreements.ItemHeight = 15;
            listBoxProposedTradeAgreements.Location = new Point(12, 242);
            listBoxProposedTradeAgreements.Margin = new Padding(4, 3, 4, 3);
            listBoxProposedTradeAgreements.Name = "listBoxProposedTradeAgreements";
            listBoxProposedTradeAgreements.Size = new Size(443, 169);
            listBoxProposedTradeAgreements.TabIndex = 1;
            // 
            // buttonAcceptTrade
            // 
            buttonAcceptTrade.Location = new Point(12, 427);
            buttonAcceptTrade.Margin = new Padding(4, 3, 4, 3);
            buttonAcceptTrade.Name = "buttonAcceptTrade";
            buttonAcceptTrade.Size = new Size(105, 27);
            buttonAcceptTrade.TabIndex = 2;
            buttonAcceptTrade.Text = "Accept Trade";
            buttonAcceptTrade.UseVisualStyleBackColor = true;
            buttonAcceptTrade.Click += ButtonAcceptTrade_Click;
            // 
            // buttonRejectTrade
            // 
            buttonRejectTrade.Location = new Point(128, 427);
            buttonRejectTrade.Margin = new Padding(4, 3, 4, 3);
            buttonRejectTrade.Name = "buttonRejectTrade";
            buttonRejectTrade.Size = new Size(105, 27);
            buttonRejectTrade.TabIndex = 3;
            buttonRejectTrade.Text = "Reject Trade";
            buttonRejectTrade.UseVisualStyleBackColor = true;
            buttonRejectTrade.Click += ButtonRejectTrade_Click;
            // 
            // buttonProposeTrade
            // 
            buttonProposeTrade.Location = new Point(525, 35);
            buttonProposeTrade.Margin = new Padding(4, 3, 4, 3);
            buttonProposeTrade.Name = "buttonProposeTrade";
            buttonProposeTrade.Size = new Size(140, 27);
            buttonProposeTrade.TabIndex = 4;
            buttonProposeTrade.Text = "Propose New Trade";
            buttonProposeTrade.UseVisualStyleBackColor = true;
            buttonProposeTrade.Click += ButtonProposeTrade_Click;
            // 
            // buttonViewRelations
            // 
            buttonViewRelations.Location = new Point(525, 69);
            buttonViewRelations.Margin = new Padding(4, 3, 4, 3);
            buttonViewRelations.Name = "buttonViewRelations";
            buttonViewRelations.Size = new Size(140, 27);
            buttonViewRelations.TabIndex = 5;
            buttonViewRelations.Text = "View Relations";
            buttonViewRelations.UseVisualStyleBackColor = true;
            buttonViewRelations.Click += ButtonViewRelations_Click;
            // 
            // buttonOpenTradeManagement
            // 
            buttonOpenTradeManagement.Location = new Point(525, 104);
            buttonOpenTradeManagement.Margin = new Padding(4, 3, 4, 3);
            buttonOpenTradeManagement.Name = "buttonOpenTradeManagement";
            buttonOpenTradeManagement.Size = new Size(140, 27);
            buttonOpenTradeManagement.TabIndex = 6;
            buttonOpenTradeManagement.Text = "Trade Management";
            buttonOpenTradeManagement.UseVisualStyleBackColor = true;
            buttonOpenTradeManagement.Click += ButtonOpenTradeManagement_Click;
            // 
            // tabPageCity
            // 
            tabPageCity.Controls.Add(comboBoxCountry);
            tabPageCity.Controls.Add(labelCountryStats);
            tabPageCity.Controls.Add(comboBoxStates);
            tabPageCity.Controls.Add(labelStateStats);
            tabPageCity.Controls.Add(comboBoxCities);
            tabPageCity.Controls.Add(listBoxBuyOrders);
            tabPageCity.Controls.Add(listBoxSellOrders);
            tabPageCity.Controls.Add(listBoxCityStats);
            tabPageCity.Controls.Add(listBoxFactoryStats);
            tabPageCity.Location = new Point(4, 24);
            tabPageCity.Margin = new Padding(4, 3, 4, 3);
            tabPageCity.Name = "tabPageCity";
            tabPageCity.Size = new Size(960, 526);
            tabPageCity.TabIndex = 2;
            tabPageCity.Text = "Economy";
            // 
            // comboBoxCountry
            // 
            comboBoxCountry.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCountry.FormattingEnabled = true;
            comboBoxCountry.Location = new Point(35, 12);
            comboBoxCountry.Margin = new Padding(4, 3, 4, 3);
            comboBoxCountry.Name = "comboBoxCountry";
            comboBoxCountry.Size = new Size(233, 23);
            comboBoxCountry.TabIndex = 0;
            // 
            // labelCountryStats
            // 
            labelCountryStats.Location = new Point(292, 12);
            labelCountryStats.Margin = new Padding(4, 0, 4, 0);
            labelCountryStats.Name = "labelCountryStats";
            labelCountryStats.Size = new Size(642, 24);
            labelCountryStats.TabIndex = 1;
            // 
            // comboBoxStates
            // 
            comboBoxStates.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxStates.FormattingEnabled = true;
            comboBoxStates.Location = new Point(35, 46);
            comboBoxStates.Margin = new Padding(4, 3, 4, 3);
            comboBoxStates.Name = "comboBoxStates";
            comboBoxStates.Size = new Size(233, 23);
            comboBoxStates.TabIndex = 2;
            // 
            // labelStateStats
            // 
            labelStateStats.Location = new Point(292, 46);
            labelStateStats.Margin = new Padding(4, 0, 4, 0);
            labelStateStats.Name = "labelStateStats";
            labelStateStats.Size = new Size(642, 24);
            labelStateStats.TabIndex = 3;
            // 
            // comboBoxCities
            // 
            comboBoxCities.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCities.FormattingEnabled = true;
            comboBoxCities.Location = new Point(35, 81);
            comboBoxCities.Margin = new Padding(4, 3, 4, 3);
            comboBoxCities.Name = "comboBoxCities";
            comboBoxCities.Size = new Size(233, 23);
            comboBoxCities.TabIndex = 4;
            // 
            // listBoxBuyOrders
            // 
            listBoxBuyOrders.FormattingEnabled = true;
            listBoxBuyOrders.ItemHeight = 15;
            listBoxBuyOrders.Location = new Point(12, 115);
            listBoxBuyOrders.Margin = new Padding(4, 3, 4, 3);
            listBoxBuyOrders.Name = "listBoxBuyOrders";
            listBoxBuyOrders.Size = new Size(291, 334);
            listBoxBuyOrders.TabIndex = 5;
            // 
            // listBoxSellOrders
            // 
            listBoxSellOrders.FormattingEnabled = true;
            listBoxSellOrders.ItemHeight = 15;
            listBoxSellOrders.Location = new Point(315, 115);
            listBoxSellOrders.Margin = new Padding(4, 3, 4, 3);
            listBoxSellOrders.Name = "listBoxSellOrders";
            listBoxSellOrders.Size = new Size(291, 334);
            listBoxSellOrders.TabIndex = 6;
            // 
            // listBoxCityStats
            // 
            listBoxCityStats.FormattingEnabled = true;
            listBoxCityStats.ItemHeight = 15;
            listBoxCityStats.Location = new Point(618, 115);
            listBoxCityStats.Margin = new Padding(4, 3, 4, 3);
            listBoxCityStats.Name = "listBoxCityStats";
            listBoxCityStats.Size = new Size(326, 109);
            listBoxCityStats.TabIndex = 7;
            // 
            // listBoxFactoryStats
            // 
            listBoxFactoryStats.FormattingEnabled = true;
            listBoxFactoryStats.ItemHeight = 15;
            listBoxFactoryStats.Location = new Point(618, 242);
            listBoxFactoryStats.Margin = new Padding(4, 3, 4, 3);
            listBoxFactoryStats.Name = "listBoxFactoryStats";
            listBoxFactoryStats.Size = new Size(326, 214);
            listBoxFactoryStats.TabIndex = 8;
            // 
            // tabPageCountry
            // 
            tabPageCountry.Controls.Add(panelMap);
            tabPageCountry.Location = new Point(4, 24);
            tabPageCountry.Margin = new Padding(4, 3, 4, 3);
            tabPageCountry.Name = "tabPageCountry";
            tabPageCountry.Size = new Size(960, 526);
            tabPageCountry.TabIndex = 0;
            tabPageCountry.Text = "Map";
            // 
            // panelMap
            // 
            panelMap.Controls.Add(pictureBox1);
            panelMap.Dock = DockStyle.Fill;
            panelMap.Location = new Point(0, 0);
            panelMap.Margin = new Padding(4, 3, 4, 3);
            panelMap.Name = "panelMap";
            panelMap.Size = new Size(960, 526);
            panelMap.TabIndex = 1;
            panelMap.AutoScroll = false;
            panelMap.TabStop = true;
            panelMap.KeyDown += new System.Windows.Forms.KeyEventHandler(this.panelMap_KeyDown);
            panelMap.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.PanelMap_MouseWheel);
            this.panelMap.MouseUp += new System.Windows.Forms.MouseEventHandler(this.panelMap_MouseUp_ForPanning);
            // 
            // pictureBox1
            // 
            pictureBox1.Dock = DockStyle.None;
            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Margin = new Padding(4, 3, 4, 3);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(960, 526);
            pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            pictureBox1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.PictureBox1_MouseWheel);
            // 
            // tabControlMain
            // 
            tabControlMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControlMain.Controls.Add(tabPageCountry);
            tabControlMain.Controls.Add(tabPageCity);
            tabControlMain.Controls.Add(tabPageDiplomacy);
            tabControlMain.Controls.Add(tabPageDebug);
            tabControlMain.Controls.Add(tabPageFinance);
            tabControlMain.Location = new Point(12, 12);
            tabControlMain.Margin = new Padding(4, 3, 4, 3);
            tabControlMain.Name = "tabControlMain";
            tabControlMain.SelectedIndex = 0;
            tabControlMain.Size = new Size(968, 554);
            tabControlMain.TabIndex = 0;
            // 
            // MainGame
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1082, 740);
            Controls.Add(tabControlMain);
            Controls.Add(labelSimTime);
            Controls.Add(listBoxMarketStats);
            Margin = new Padding(4, 3, 4, 3);
            Name = "MainGame";
            Text = "MainGame";
            tabPageCompanies.ResumeLayout(false);
            tabPageFinance.ResumeLayout(false);
            tabPageDebug.ResumeLayout(false);
            tabPageDebug.PerformLayout();
            tabPageDiplomacy.ResumeLayout(false);
            tabPageDiplomacy.PerformLayout();
            tabPageCity.ResumeLayout(false);
            tabPageCountry.ResumeLayout(false);
            panelMap.ResumeLayout(false);
            panelMap.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            tabControlMain.ResumeLayout(false);
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabPage tabPageFinance;
        private System.Windows.Forms.ListView listViewFinance;
        private System.Windows.Forms.TabPage tabPageDebug;
        private System.Windows.Forms.Button buttonToggleDebug;
        private System.Windows.Forms.Label labelCurrentRole;
        private System.Windows.Forms.Label labelRoleType;
        private System.Windows.Forms.ComboBox comboBoxRoleType;
        private System.Windows.Forms.Label labelEntitySelection;
        private System.Windows.Forms.ComboBox comboBoxCountrySelection;
        private System.Windows.Forms.ComboBox comboBoxStateSelection;
        private System.Windows.Forms.ComboBox comboBoxCorporationSelection;
        private System.Windows.Forms.Button buttonAssumeRole;
        private System.Windows.Forms.Button buttonRelinquishRole;
        private System.Windows.Forms.CheckBox checkBoxLogPops;
        private System.Windows.Forms.CheckBox checkBoxLogBuildings;
        private System.Windows.Forms.CheckBox checkBoxLogEconomy;
        private System.Windows.Forms.Button buttonGenerateTileCache;
        private System.Windows.Forms.TabPage tabPageDiplomacy;
        private System.Windows.Forms.Label labelProposedTrades;
        private System.Windows.Forms.ListBox listBoxProposedTradeAgreements;
        private System.Windows.Forms.Button buttonAcceptTrade;
        private System.Windows.Forms.Button buttonRejectTrade;
        private System.Windows.Forms.Button buttonProposeTrade;
        private System.Windows.Forms.Button buttonViewRelations;
        private System.Windows.Forms.Button buttonOpenTradeManagement;
        private System.Windows.Forms.TabPage tabPageCity;
        private System.Windows.Forms.ComboBox comboBoxCountry;
        private System.Windows.Forms.Label labelCountryStats;
        private System.Windows.Forms.ComboBox comboBoxStates;
        private System.Windows.Forms.Label labelStateStats;
        private System.Windows.Forms.ComboBox comboBoxCities;
        private System.Windows.Forms.ListBox listBoxBuyOrders;
        private System.Windows.Forms.ListBox listBoxSellOrders;
        private System.Windows.Forms.ListBox listBoxCityStats;
        private System.Windows.Forms.ListBox listBoxFactoryStats;
        private System.Windows.Forms.TabPage tabPageCountry;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Panel panelMap;

        private System.Windows.Forms.TabControl tabControlMain;


    }
}