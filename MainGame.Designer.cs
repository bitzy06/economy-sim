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
            listViewDiplomacy = new ListView();
            buttonShowPopStats = new Button();
            buttonShowFactoryStats = new Button();
            buttonShowConstruction = new Button();
            tabPageGovernment = new TabPage();
            listViewParties = new ListView();
            buttonOpenPolicyManager = new Button();
            buttonToggleDebugMode = new Button();
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
            tabPageGovernment.SuspendLayout();
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
            labelSimTime.Location = new Point(14, 769);
            labelSimTime.Margin = new Padding(5, 0, 5, 0);
            labelSimTime.Name = "labelSimTime";
            labelSimTime.Size = new Size(160, 36);
            labelSimTime.TabIndex = 1;
            labelSimTime.Text = "Turn: 0";
            // 
            // listBoxMarketStats
            // 
            listBoxMarketStats.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            listBoxMarketStats.FormattingEnabled = true;
            listBoxMarketStats.Location = new Point(200, 769);
            listBoxMarketStats.Margin = new Padding(5, 4, 5, 4);
            listBoxMarketStats.Name = "listBoxMarketStats";
            listBoxMarketStats.Size = new Size(918, 44);
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
            // listViewDiplomacy
            // 
            listViewDiplomacy.FullRowSelect = true;
            listViewDiplomacy.GridLines = true;
            listViewDiplomacy.Location = new Point(11, 13);
            listViewDiplomacy.Margin = new Padding(3, 4, 3, 4);
            listViewDiplomacy.Name = "listViewDiplomacy";
            listViewDiplomacy.Size = new Size(457, 239);
            listViewDiplomacy.TabIndex = 7;
            listViewDiplomacy.UseCompatibleStateImageBehavior = false;
            listViewDiplomacy.View = View.Details;
            // 
            // buttonShowPopStats
            // 
            buttonShowPopStats.Location = new Point(0, 0);
            buttonShowPopStats.Margin = new Padding(3, 4, 3, 4);
            buttonShowPopStats.Name = "buttonShowPopStats";
            buttonShowPopStats.Size = new Size(137, 31);
            buttonShowPopStats.TabIndex = 9;
            buttonShowPopStats.Text = "Show Pop Stats";
            // 
            // buttonShowFactoryStats
            // 
            buttonShowFactoryStats.Location = new Point(0, 0);
            buttonShowFactoryStats.Margin = new Padding(3, 4, 3, 4);
            buttonShowFactoryStats.Name = "buttonShowFactoryStats";
            buttonShowFactoryStats.Size = new Size(137, 31);
            buttonShowFactoryStats.TabIndex = 10;
            buttonShowFactoryStats.Text = "Building Details";
            // 
            // buttonShowConstruction
            // 
            buttonShowConstruction.Location = new Point(0, 0);
            buttonShowConstruction.Margin = new Padding(3, 4, 3, 4);
            buttonShowConstruction.Name = "buttonShowConstruction";
            buttonShowConstruction.Size = new Size(137, 31);
            buttonShowConstruction.TabIndex = 11;
            buttonShowConstruction.Text = "Construction";
            // 
            // tabPageGovernment
            // 
            tabPageGovernment.Controls.Add(listViewParties);
            tabPageGovernment.Controls.Add(buttonOpenPolicyManager);
            tabPageGovernment.Location = new Point(4, 29);
            tabPageGovernment.Margin = new Padding(5, 4, 5, 4);
            tabPageGovernment.Name = "tabPageGovernment";
            tabPageGovernment.Size = new Size(1098, 706);
            tabPageGovernment.TabIndex = 6;
            tabPageGovernment.Text = "Government";
            tabPageGovernment.UseVisualStyleBackColor = true;
            // 
            // listViewParties
            // 
            listViewParties.FullRowSelect = true;
            listViewParties.GridLines = true;
            listViewParties.Location = new Point(11, 13);
            listViewParties.Margin = new Padding(3, 4, 3, 4);
            listViewParties.Name = "listViewParties";
            listViewParties.Size = new Size(457, 265);
            listViewParties.TabIndex = 0;
            listViewParties.UseCompatibleStateImageBehavior = false;
            listViewParties.View = View.Details;
            // 
            // buttonOpenPolicyManager
            // 
            buttonOpenPolicyManager.Location = new Point(11, 293);
            buttonOpenPolicyManager.Margin = new Padding(3, 4, 3, 4);
            buttonOpenPolicyManager.Name = "buttonOpenPolicyManager";
            buttonOpenPolicyManager.Size = new Size(171, 40);
            buttonOpenPolicyManager.TabIndex = 1;
            buttonOpenPolicyManager.Text = "Open Policy Manager";
            buttonOpenPolicyManager.Click += ButtonOpenPolicyManager_Click;
            // 
            // buttonToggleDebugMode
            // 
            buttonToggleDebugMode.Location = new Point(14, 389);
            buttonToggleDebugMode.Margin = new Padding(3, 4, 3, 4);
            buttonToggleDebugMode.Name = "buttonToggleDebugMode";
            buttonToggleDebugMode.Size = new Size(137, 31);
            buttonToggleDebugMode.TabIndex = 14;
            buttonToggleDebugMode.Text = "Toggle Debug Mode";
            // 
            // tabPageFinance
            // 
            tabPageFinance.Controls.Add(listViewFinance);
            tabPageFinance.Location = new Point(4, 29);
            tabPageFinance.Margin = new Padding(5, 4, 5, 4);
            tabPageFinance.Name = "tabPageFinance";
            tabPageFinance.Size = new Size(1098, 706);
            tabPageFinance.TabIndex = 5;
            tabPageFinance.Text = "Finance";
            tabPageFinance.UseVisualStyleBackColor = true;
            // 
            // listViewFinance
            // 
            listViewFinance.FullRowSelect = true;
            listViewFinance.GridLines = true;
            listViewFinance.Location = new Point(14, 16);
            listViewFinance.Margin = new Padding(5, 4, 5, 4);
            listViewFinance.Name = "listViewFinance";
            listViewFinance.Size = new Size(1066, 660);
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
            tabPageDebug.Controls.Add(buttonToggleDebugMode);
            tabPageDebug.Location = new Point(4, 29);
            tabPageDebug.Margin = new Padding(5, 4, 5, 4);
            tabPageDebug.Name = "tabPageDebug";
            tabPageDebug.Padding = new Padding(5, 4, 5, 4);
            tabPageDebug.Size = new Size(1098, 706);
            tabPageDebug.TabIndex = 4;
            tabPageDebug.Text = "Debug";
            tabPageDebug.UseVisualStyleBackColor = true;
            // 
            // buttonToggleDebug
            // 
            buttonToggleDebug.Location = new Point(14, 16);
            buttonToggleDebug.Margin = new Padding(5, 4, 5, 4);
            buttonToggleDebug.Name = "buttonToggleDebug";
            buttonToggleDebug.Size = new Size(160, 36);
            buttonToggleDebug.TabIndex = 0;
            buttonToggleDebug.Text = "Toggle Debug";
            buttonToggleDebug.UseVisualStyleBackColor = true;
            buttonToggleDebug.Click += ButtonToggleDebug_Click;
            // 
            // labelCurrentRole
            // 
            labelCurrentRole.AutoSize = true;
            labelCurrentRole.Location = new Point(14, 77);
            labelCurrentRole.Margin = new Padding(5, 0, 5, 0);
            labelCurrentRole.Name = "labelCurrentRole";
            labelCurrentRole.Size = new Size(94, 20);
            labelCurrentRole.TabIndex = 1;
            labelCurrentRole.Text = "Current Role:";
            // 
            // labelRoleType
            // 
            labelRoleType.AutoSize = true;
            labelRoleType.Location = new Point(14, 123);
            labelRoleType.Margin = new Padding(5, 0, 5, 0);
            labelRoleType.Name = "labelRoleType";
            labelRoleType.Size = new Size(77, 20);
            labelRoleType.TabIndex = 2;
            labelRoleType.Text = "Role Type:";
            // 
            // comboBoxRoleType
            // 
            comboBoxRoleType.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxRoleType.FormattingEnabled = true;
            comboBoxRoleType.Location = new Point(106, 123);
            comboBoxRoleType.Margin = new Padding(5, 4, 5, 4);
            comboBoxRoleType.Name = "comboBoxRoleType";
            comboBoxRoleType.Size = new Size(266, 28);
            comboBoxRoleType.TabIndex = 3;
            // 
            // labelEntitySelection
            // 
            labelEntitySelection.AutoSize = true;
            labelEntitySelection.Location = new Point(14, 169);
            labelEntitySelection.Margin = new Padding(5, 0, 5, 0);
            labelEntitySelection.Name = "labelEntitySelection";
            labelEntitySelection.Size = new Size(114, 20);
            labelEntitySelection.TabIndex = 4;
            labelEntitySelection.Text = "Entity Selection:";
            // 
            // comboBoxCountrySelection
            // 
            comboBoxCountrySelection.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCountrySelection.FormattingEnabled = true;
            comboBoxCountrySelection.Location = new Point(14, 200);
            comboBoxCountrySelection.Margin = new Padding(5, 4, 5, 4);
            comboBoxCountrySelection.Name = "comboBoxCountrySelection";
            comboBoxCountrySelection.Size = new Size(266, 28);
            comboBoxCountrySelection.TabIndex = 5;
            // 
            // comboBoxStateSelection
            // 
            comboBoxStateSelection.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxStateSelection.FormattingEnabled = true;
            comboBoxStateSelection.Location = new Point(14, 247);
            comboBoxStateSelection.Margin = new Padding(5, 4, 5, 4);
            comboBoxStateSelection.Name = "comboBoxStateSelection";
            comboBoxStateSelection.Size = new Size(266, 28);
            comboBoxStateSelection.TabIndex = 6;
            // 
            // comboBoxCorporationSelection
            // 
            comboBoxCorporationSelection.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCorporationSelection.FormattingEnabled = true;
            comboBoxCorporationSelection.Location = new Point(14, 292);
            comboBoxCorporationSelection.Margin = new Padding(5, 4, 5, 4);
            comboBoxCorporationSelection.Name = "comboBoxCorporationSelection";
            comboBoxCorporationSelection.Size = new Size(266, 28);
            comboBoxCorporationSelection.TabIndex = 7;
            // 
            // buttonAssumeRole
            // 
            buttonAssumeRole.Location = new Point(11, 615);
            buttonAssumeRole.Margin = new Padding(5, 4, 5, 4);
            buttonAssumeRole.Name = "buttonAssumeRole";
            buttonAssumeRole.Size = new Size(160, 36);
            buttonAssumeRole.TabIndex = 8;
            buttonAssumeRole.Text = "Assume Role";
            buttonAssumeRole.UseVisualStyleBackColor = true;
            buttonAssumeRole.Click += ButtonAssumeRole_Click;
            // 
            // buttonRelinquishRole
            // 
            buttonRelinquishRole.Location = new Point(10, 662);
            buttonRelinquishRole.Margin = new Padding(5, 4, 5, 4);
            buttonRelinquishRole.Name = "buttonRelinquishRole";
            buttonRelinquishRole.Size = new Size(160, 36);
            buttonRelinquishRole.TabIndex = 9;
            buttonRelinquishRole.Text = "Relinquish Role";
            buttonRelinquishRole.UseVisualStyleBackColor = true;
            buttonRelinquishRole.Click += ButtonRelinquishRole_Click;
            // 
            // checkBoxLogPops
            // 
            checkBoxLogPops.AutoSize = true;
            checkBoxLogPops.Location = new Point(14, 428);
            checkBoxLogPops.Margin = new Padding(5, 4, 5, 4);
            checkBoxLogPops.Name = "checkBoxLogPops";
            checkBoxLogPops.Size = new Size(121, 24);
            checkBoxLogPops.TabIndex = 10;
            checkBoxLogPops.Text = "Log Pop Stats";
            checkBoxLogPops.UseVisualStyleBackColor = true;
            checkBoxLogPops.CheckedChanged += CheckBoxLogPops_CheckedChanged;
            // 
            // checkBoxLogBuildings
            // 
            checkBoxLogBuildings.AutoSize = true;
            checkBoxLogBuildings.Location = new Point(14, 458);
            checkBoxLogBuildings.Margin = new Padding(5, 4, 5, 4);
            checkBoxLogBuildings.Name = "checkBoxLogBuildings";
            checkBoxLogBuildings.Size = new Size(151, 24);
            checkBoxLogBuildings.TabIndex = 11;
            checkBoxLogBuildings.Text = "Log Building Stats";
            checkBoxLogBuildings.UseVisualStyleBackColor = true;
            checkBoxLogBuildings.CheckedChanged += CheckBoxLogBuildings_CheckedChanged;
            // 
            // checkBoxLogEconomy
            // 
            checkBoxLogEconomy.AutoSize = true;
            checkBoxLogEconomy.Location = new Point(14, 489);
            checkBoxLogEconomy.Margin = new Padding(5, 4, 5, 4);
            checkBoxLogEconomy.Name = "checkBoxLogEconomy";
            checkBoxLogEconomy.Size = new Size(157, 24);
            checkBoxLogEconomy.TabIndex = 12;
            checkBoxLogEconomy.Text = "Log Economy Stats";
            checkBoxLogEconomy.UseVisualStyleBackColor = true;
            checkBoxLogEconomy.CheckedChanged += CheckBoxLogEconomy_CheckedChanged;
            // 
            // buttonGenerateTileCache
            //
            buttonGenerateTileCache.Location = new Point(182, 662);
            buttonGenerateTileCache.Margin = new Padding(5, 4, 5, 4);
            buttonGenerateTileCache.Name = "buttonGenerateTileCache";
            buttonGenerateTileCache.Size = new Size(160, 36);
            buttonGenerateTileCache.TabIndex = 13;
            buttonGenerateTileCache.Text = "Build Tile Cache";
            buttonGenerateTileCache.UseVisualStyleBackColor = true;

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
            tabPageDiplomacy.Controls.Add(listViewDiplomacy);
            tabPageDiplomacy.Location = new Point(4, 29);
            tabPageDiplomacy.Margin = new Padding(5, 4, 5, 4);
            tabPageDiplomacy.Name = "tabPageDiplomacy";
            tabPageDiplomacy.Padding = new Padding(5, 4, 5, 4);
            tabPageDiplomacy.Size = new Size(1098, 706);
            tabPageDiplomacy.TabIndex = 3;
            tabPageDiplomacy.Text = "Diplomacy";
            tabPageDiplomacy.UseVisualStyleBackColor = true;
            // 
            // labelProposedTrades
            // 
            labelProposedTrades.AutoSize = true;
            labelProposedTrades.Location = new Point(14, 292);
            labelProposedTrades.Margin = new Padding(5, 0, 5, 0);
            labelProposedTrades.Name = "labelProposedTrades";
            labelProposedTrades.Size = new Size(256, 20);
            labelProposedTrades.TabIndex = 0;
            labelProposedTrades.Text = "Proposed Trade Agreements (to you):";
            // 
            // listBoxProposedTradeAgreements
            // 
            listBoxProposedTradeAgreements.FormattingEnabled = true;
            listBoxProposedTradeAgreements.Location = new Point(14, 323);
            listBoxProposedTradeAgreements.Margin = new Padding(5, 4, 5, 4);
            listBoxProposedTradeAgreements.Name = "listBoxProposedTradeAgreements";
            listBoxProposedTradeAgreements.Size = new Size(506, 224);
            listBoxProposedTradeAgreements.TabIndex = 1;
            // 
            // buttonAcceptTrade
            // 
            buttonAcceptTrade.Location = new Point(14, 569);
            buttonAcceptTrade.Margin = new Padding(5, 4, 5, 4);
            buttonAcceptTrade.Name = "buttonAcceptTrade";
            buttonAcceptTrade.Size = new Size(120, 36);
            buttonAcceptTrade.TabIndex = 2;
            buttonAcceptTrade.Text = "Accept Trade";
            buttonAcceptTrade.UseVisualStyleBackColor = true;
            buttonAcceptTrade.Click += ButtonAcceptTrade_Click;
            // 
            // buttonRejectTrade
            // 
            buttonRejectTrade.Location = new Point(146, 569);
            buttonRejectTrade.Margin = new Padding(5, 4, 5, 4);
            buttonRejectTrade.Name = "buttonRejectTrade";
            buttonRejectTrade.Size = new Size(120, 36);
            buttonRejectTrade.TabIndex = 3;
            buttonRejectTrade.Text = "Reject Trade";
            buttonRejectTrade.UseVisualStyleBackColor = true;
            buttonRejectTrade.Click += ButtonRejectTrade_Click;
            // 
            // buttonProposeTrade
            // 
            buttonProposeTrade.Location = new Point(600, 47);
            buttonProposeTrade.Margin = new Padding(5, 4, 5, 4);
            buttonProposeTrade.Name = "buttonProposeTrade";
            buttonProposeTrade.Size = new Size(160, 36);
            buttonProposeTrade.TabIndex = 4;
            buttonProposeTrade.Text = "Propose New Trade";
            buttonProposeTrade.UseVisualStyleBackColor = true;
            buttonProposeTrade.Click += ButtonProposeTrade_Click;
            // 
            // buttonViewRelations
            // 
            buttonViewRelations.Location = new Point(600, 92);
            buttonViewRelations.Margin = new Padding(5, 4, 5, 4);
            buttonViewRelations.Name = "buttonViewRelations";
            buttonViewRelations.Size = new Size(160, 36);
            buttonViewRelations.TabIndex = 5;
            buttonViewRelations.Text = "View Relations";
            buttonViewRelations.UseVisualStyleBackColor = true;
            buttonViewRelations.Click += ButtonViewRelations_Click;
            // 
            // buttonOpenTradeManagement
            // 
            buttonOpenTradeManagement.Location = new Point(600, 139);
            buttonOpenTradeManagement.Margin = new Padding(5, 4, 5, 4);
            buttonOpenTradeManagement.Name = "buttonOpenTradeManagement";
            buttonOpenTradeManagement.Size = new Size(160, 36);
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
            tabPageCity.Controls.Add(buttonShowPopStats);
            tabPageCity.Controls.Add(buttonShowFactoryStats);
            tabPageCity.Controls.Add(buttonShowConstruction);
            tabPageCity.Location = new Point(4, 29);
            tabPageCity.Margin = new Padding(5, 4, 5, 4);
            tabPageCity.Name = "tabPageCity";
            tabPageCity.Size = new Size(1098, 706);
            tabPageCity.TabIndex = 2;
            tabPageCity.Text = "Economy";
            // 
            // comboBoxCountry
            // 
            comboBoxCountry.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCountry.FormattingEnabled = true;
            comboBoxCountry.Location = new Point(40, 16);
            comboBoxCountry.Margin = new Padding(5, 4, 5, 4);
            comboBoxCountry.Name = "comboBoxCountry";
            comboBoxCountry.Size = new Size(266, 28);
            comboBoxCountry.TabIndex = 0;
            // 
            // labelCountryStats
            // 
            labelCountryStats.Location = new Point(334, 16);
            labelCountryStats.Margin = new Padding(5, 0, 5, 0);
            labelCountryStats.Name = "labelCountryStats";
            labelCountryStats.Size = new Size(734, 32);
            labelCountryStats.TabIndex = 1;
            // 
            // comboBoxStates
            // 
            comboBoxStates.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxStates.FormattingEnabled = true;
            comboBoxStates.Location = new Point(40, 61);
            comboBoxStates.Margin = new Padding(5, 4, 5, 4);
            comboBoxStates.Name = "comboBoxStates";
            comboBoxStates.Size = new Size(266, 28);
            comboBoxStates.TabIndex = 2;
            // 
            // labelStateStats
            // 
            labelStateStats.Location = new Point(334, 61);
            labelStateStats.Margin = new Padding(5, 0, 5, 0);
            labelStateStats.Name = "labelStateStats";
            labelStateStats.Size = new Size(734, 32);
            labelStateStats.TabIndex = 3;
            // 
            // comboBoxCities
            // 
            comboBoxCities.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxCities.FormattingEnabled = true;
            comboBoxCities.Location = new Point(40, 108);
            comboBoxCities.Margin = new Padding(5, 4, 5, 4);
            comboBoxCities.Name = "comboBoxCities";
            comboBoxCities.Size = new Size(266, 28);
            comboBoxCities.TabIndex = 4;
            // 
            // listBoxBuyOrders
            // 
            listBoxBuyOrders.FormattingEnabled = true;
            listBoxBuyOrders.Location = new Point(14, 153);
            listBoxBuyOrders.Margin = new Padding(5, 4, 5, 4);
            listBoxBuyOrders.Name = "listBoxBuyOrders";
            listBoxBuyOrders.Size = new Size(332, 444);
            listBoxBuyOrders.TabIndex = 5;
            // 
            // listBoxSellOrders
            // 
            listBoxSellOrders.FormattingEnabled = true;
            listBoxSellOrders.Location = new Point(360, 153);
            listBoxSellOrders.Margin = new Padding(5, 4, 5, 4);
            listBoxSellOrders.Name = "listBoxSellOrders";
            listBoxSellOrders.Size = new Size(332, 444);
            listBoxSellOrders.TabIndex = 6;
            // 
            // listBoxCityStats
            // 
            listBoxCityStats.FormattingEnabled = true;
            listBoxCityStats.Location = new Point(706, 153);
            listBoxCityStats.Margin = new Padding(5, 4, 5, 4);
            listBoxCityStats.Name = "listBoxCityStats";
            listBoxCityStats.Size = new Size(372, 144);
            listBoxCityStats.TabIndex = 7;
            // 
            // listBoxFactoryStats
            // 
            listBoxFactoryStats.FormattingEnabled = true;
            listBoxFactoryStats.Location = new Point(706, 323);
            listBoxFactoryStats.Margin = new Padding(5, 4, 5, 4);
            listBoxFactoryStats.Name = "listBoxFactoryStats";
            listBoxFactoryStats.Size = new Size(372, 284);
            listBoxFactoryStats.TabIndex = 8;
            // 
            // tabPageCountry
            // 
            tabPageCountry.Controls.Add(panelMap);
            tabPageCountry.Location = new Point(4, 29);
            tabPageCountry.Margin = new Padding(5, 4, 5, 4);
            tabPageCountry.Name = "tabPageCountry";
            tabPageCountry.Size = new Size(1098, 706);
            tabPageCountry.TabIndex = 0;
            tabPageCountry.Text = "Map";
            // 
            // panelMap
            // 
            panelMap.Controls.Add(pictureBox1);
            panelMap.Dock = DockStyle.Fill;
            panelMap.Location = new Point(0, 0);
            panelMap.Margin = new Padding(5, 4, 5, 4);
            panelMap.Name = "panelMap";
            panelMap.Size = new Size(1098, 706);
            panelMap.TabIndex = 1;
            panelMap.TabStop = true;
            panelMap.KeyDown += panelMap_KeyDown;
            panelMap.MouseUp += panelMap_MouseUp_ForPanning;
            // 
            // pictureBox1
            // 
            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Margin = new Padding(5, 4, 5, 4);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(1097, 701);
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // tabControlMain
            // 
            tabControlMain.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControlMain.Controls.Add(tabPageCountry);
            tabControlMain.Controls.Add(tabPageCity);
            tabControlMain.Controls.Add(tabPageDiplomacy);
            tabControlMain.Controls.Add(tabPageGovernment);
            tabControlMain.Controls.Add(tabPageDebug);
            tabControlMain.Controls.Add(tabPageFinance);
            tabControlMain.Location = new Point(14, 16);
            tabControlMain.Margin = new Padding(5, 4, 5, 4);
            tabControlMain.Name = "tabControlMain";
            tabControlMain.SelectedIndex = 0;
            tabControlMain.Size = new Size(1106, 739);
            tabControlMain.TabIndex = 0;
            // 
            // MainGame
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1237, 987);
            Controls.Add(tabControlMain);
            Controls.Add(labelSimTime);
            Controls.Add(listBoxMarketStats);
            Margin = new Padding(5, 4, 5, 4);
            Name = "MainGame";
            Text = "MainGame";
            tabPageCompanies.ResumeLayout(false);
            tabPageGovernment.ResumeLayout(false);
            tabPageFinance.ResumeLayout(false);
            tabPageDebug.ResumeLayout(false);
            tabPageDebug.PerformLayout();
            tabPageDiplomacy.ResumeLayout(false);
            tabPageDiplomacy.PerformLayout();
            tabPageCity.ResumeLayout(false);
            tabPageCountry.ResumeLayout(false);
            panelMap.ResumeLayout(false);
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
        private System.Windows.Forms.ListView listViewDiplomacy;
        private System.Windows.Forms.Button buttonShowPopStats;
        private System.Windows.Forms.Button buttonShowFactoryStats;
        private System.Windows.Forms.Button buttonShowConstruction;
        private System.Windows.Forms.TabPage tabPageGovernment;
        private System.Windows.Forms.ListView listViewParties;
        private System.Windows.Forms.Button buttonOpenPolicyManager;
        private System.Windows.Forms.Button buttonToggleDebugMode;
        private System.Windows.Forms.TabPage tabPageCountry;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Panel panelMap;

        private System.Windows.Forms.TabControl tabControlMain;


    }
}