namespace economy_sim
{
    partial class MainGame
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ComboBox comboBoxCities;
        private System.Windows.Forms.ListBox listBoxBuyOrders;
        private System.Windows.Forms.ListBox listBoxSellOrders;
        private System.Windows.Forms.Timer timerSim;
        private System.Windows.Forms.Label labelSimTime;
        private System.Windows.Forms.ListBox listBoxCityStats;
        private System.Windows.Forms.ListBox listBoxFactoryStats;
        private System.Windows.Forms.ListBox listBoxMarketStats;
        private System.Windows.Forms.ComboBox comboBoxCountry;
        private System.Windows.Forms.Label labelCountryStats;
        private System.Windows.Forms.ComboBox comboBoxStates;
        private System.Windows.Forms.Label labelStateStats;
        private System.Windows.Forms.TabControl tabControlMain;
        private System.Windows.Forms.TabPage tabPageCountry;
        private System.Windows.Forms.TabPage tabPageState;
        private System.Windows.Forms.TabPage tabPageCity;
        private System.Windows.Forms.TabPage tabPageDiplomacy; // Added
        private System.Windows.Forms.TabPage tabPageDebug; // Debug tab for role switching
        private System.Windows.Forms.TabPage tabPageFinance;
        private System.Windows.Forms.ListView listViewFinance;
        // private System.Windows.Forms.ListBox listBoxActiveTradeAgreements; // REMOVED by commenting
        private System.Windows.Forms.ListBox listBoxProposedTradeAgreements; // Added
        private System.Windows.Forms.Button buttonAcceptTrade; // Added
        private System.Windows.Forms.Button buttonRejectTrade; // Added
        private System.Windows.Forms.Button buttonProposeTrade; // Added
        private System.Windows.Forms.Button buttonViewRelations; // Added
        private System.Windows.Forms.Button buttonOpenTradeManagement; // Added for enhanced trade system
        // private System.Windows.Forms.Label labelActiveTrades; // REMOVED by commenting
        private System.Windows.Forms.Label labelProposedTrades; // Added
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
            this.components = new System.ComponentModel.Container();
            this.comboBoxCountry = new System.Windows.Forms.ComboBox();
            this.labelCountryStats = new System.Windows.Forms.Label();
            this.comboBoxStates = new System.Windows.Forms.ComboBox();
            this.labelStateStats = new System.Windows.Forms.Label();
            this.comboBoxCities = new System.Windows.Forms.ComboBox();
            this.listBoxBuyOrders = new System.Windows.Forms.ListBox();
            this.listBoxSellOrders = new System.Windows.Forms.ListBox();
            this.timerSim = new System.Windows.Forms.Timer(this.components);
            this.labelSimTime = new System.Windows.Forms.Label();
            this.listBoxCityStats = new System.Windows.Forms.ListBox();
            this.listBoxFactoryStats = new System.Windows.Forms.ListBox();
            this.listBoxMarketStats = new System.Windows.Forms.ListBox();
            this.tabControlMain = new System.Windows.Forms.TabControl();
            this.tabPageCountry = new System.Windows.Forms.TabPage();
            this.tabPageState = new System.Windows.Forms.TabPage();
            this.tabPageCity = new System.Windows.Forms.TabPage();
            this.tabPageDiplomacy = new System.Windows.Forms.TabPage();
            this.labelProposedTrades = new System.Windows.Forms.Label();
            this.listBoxProposedTradeAgreements = new System.Windows.Forms.ListBox();
            this.buttonAcceptTrade = new System.Windows.Forms.Button();
            this.buttonRejectTrade = new System.Windows.Forms.Button();
            this.buttonProposeTrade = new System.Windows.Forms.Button();
            this.buttonViewRelations = new System.Windows.Forms.Button();
            this.buttonOpenTradeManagement = new System.Windows.Forms.Button();
            this.tabPageDebug = new System.Windows.Forms.TabPage();
            this.buttonToggleDebug = new System.Windows.Forms.Button();
            this.labelCurrentRole = new System.Windows.Forms.Label();
            this.labelRoleType = new System.Windows.Forms.Label();
            this.comboBoxRoleType = new System.Windows.Forms.ComboBox();
            this.labelEntitySelection = new System.Windows.Forms.Label();
            this.comboBoxCountrySelection = new System.Windows.Forms.ComboBox();
            this.comboBoxStateSelection = new System.Windows.Forms.ComboBox();
            this.comboBoxCorporationSelection = new System.Windows.Forms.ComboBox();
            this.buttonAssumeRole = new System.Windows.Forms.Button();
            this.buttonRelinquishRole = new System.Windows.Forms.Button();
            this.checkBoxLogPops = new System.Windows.Forms.CheckBox();
            this.checkBoxLogBuildings = new System.Windows.Forms.CheckBox();
            this.checkBoxLogEconomy = new System.Windows.Forms.CheckBox();
            this.tabPageFinance = new System.Windows.Forms.TabPage();
            this.listViewFinance = new System.Windows.Forms.ListView();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.tabControlMain.SuspendLayout();
            this.tabPageCountry.SuspendLayout();
            this.tabPageCity.SuspendLayout();
            this.tabPageDiplomacy.SuspendLayout();
            this.tabPageDebug.SuspendLayout();
            this.tabPageFinance.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // comboBoxCountry
            // 
            this.comboBoxCountry.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCountry.FormattingEnabled = true;
            this.comboBoxCountry.Location = new System.Drawing.Point(30, 10);
            this.comboBoxCountry.Name = "comboBoxCountry";
            this.comboBoxCountry.Size = new System.Drawing.Size(200, 21);
            this.comboBoxCountry.TabIndex = 0;
            // 
            // labelCountryStats
            // 
            this.labelCountryStats.Location = new System.Drawing.Point(250, 10);
            this.labelCountryStats.Name = "labelCountryStats";
            this.labelCountryStats.Size = new System.Drawing.Size(550, 21);
            this.labelCountryStats.TabIndex = 1;
            // 
            // comboBoxStates
            // 
            this.comboBoxStates.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStates.FormattingEnabled = true;
            this.comboBoxStates.Location = new System.Drawing.Point(30, 40);
            this.comboBoxStates.Name = "comboBoxStates";
            this.comboBoxStates.Size = new System.Drawing.Size(200, 21);
            this.comboBoxStates.TabIndex = 2;
            // 
            // labelStateStats
            // 
            this.labelStateStats.Location = new System.Drawing.Point(250, 40);
            this.labelStateStats.Name = "labelStateStats";
            this.labelStateStats.Size = new System.Drawing.Size(550, 21);
            this.labelStateStats.TabIndex = 3;
            // 
            // comboBoxCities
            // 
            this.comboBoxCities.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCities.FormattingEnabled = true;
            this.comboBoxCities.Location = new System.Drawing.Point(30, 70);
            this.comboBoxCities.Name = "comboBoxCities";
            this.comboBoxCities.Size = new System.Drawing.Size(200, 21);
            this.comboBoxCities.TabIndex = 4;
            // 
            // listBoxBuyOrders
            // 
            this.listBoxBuyOrders.FormattingEnabled = true;
            this.listBoxBuyOrders.Location = new System.Drawing.Point(10, 100);
            this.listBoxBuyOrders.Name = "listBoxBuyOrders";
            this.listBoxBuyOrders.Size = new System.Drawing.Size(250, 290);
            this.listBoxBuyOrders.TabIndex = 5;
            // 
            // listBoxSellOrders
            // 
            this.listBoxSellOrders.FormattingEnabled = true;
            this.listBoxSellOrders.Location = new System.Drawing.Point(270, 100);
            this.listBoxSellOrders.Name = "listBoxSellOrders";
            this.listBoxSellOrders.Size = new System.Drawing.Size(250, 290);
            this.listBoxSellOrders.TabIndex = 6;
            // 
            // timerSim
            // 
            this.timerSim.Interval = 1000;
            // 
            // labelSimTime
            // 
            this.labelSimTime.Location = new System.Drawing.Point(10, 500);
            this.labelSimTime.Name = "labelSimTime";
            this.labelSimTime.Size = new System.Drawing.Size(120, 23);
            this.labelSimTime.TabIndex = 1;
            this.labelSimTime.Text = "Turn: 0";
            // 
            // listBoxCityStats
            // 
            this.listBoxCityStats.FormattingEnabled = true;
            this.listBoxCityStats.Location = new System.Drawing.Point(530, 100);
            this.listBoxCityStats.Name = "listBoxCityStats";
            this.listBoxCityStats.Size = new System.Drawing.Size(280, 95);
            this.listBoxCityStats.TabIndex = 7;
            // 
            // listBoxFactoryStats
            // 
            this.listBoxFactoryStats.FormattingEnabled = true;
            this.listBoxFactoryStats.Location = new System.Drawing.Point(530, 210);
            this.listBoxFactoryStats.Name = "listBoxFactoryStats";
            this.listBoxFactoryStats.Size = new System.Drawing.Size(280, 186);
            this.listBoxFactoryStats.TabIndex = 8;
            // 
            // listBoxMarketStats
            // 
            this.listBoxMarketStats.FormattingEnabled = true;
            this.listBoxMarketStats.Location = new System.Drawing.Point(150, 500);
            this.listBoxMarketStats.Name = "listBoxMarketStats";
            this.listBoxMarketStats.Size = new System.Drawing.Size(690, 30);
            this.listBoxMarketStats.TabIndex = 2;
            // 
            // tabControlMain
            // 
            this.tabControlMain.Controls.Add(this.tabPageCountry);
            this.tabControlMain.Controls.Add(this.tabPageState);
            this.tabControlMain.Controls.Add(this.tabPageCity);
            this.tabControlMain.Controls.Add(this.tabPageDiplomacy);
            this.tabControlMain.Controls.Add(this.tabPageDebug);
            this.tabControlMain.Controls.Add(this.tabPageFinance);
            this.tabControlMain.Location = new System.Drawing.Point(10, 10);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(830, 480);
            this.tabControlMain.TabIndex = 0;
            // 
            // tabPageCountry
            // 
            this.tabPageCountry.Controls.Add(this.pictureBox1);
            this.tabPageCountry.Location = new System.Drawing.Point(4, 22);
            this.tabPageCountry.Name = "tabPageCountry";
            this.tabPageCountry.Size = new System.Drawing.Size(822, 454);
            this.tabPageCountry.TabIndex = 0;
            this.tabPageCountry.Text = "Map";
            // 
            // tabPageState
            // 
            this.tabPageState.Location = new System.Drawing.Point(4, 22);
            this.tabPageState.Name = "tabPageState";
            this.tabPageState.Size = new System.Drawing.Size(822, 454);
            this.tabPageState.TabIndex = 1;
            this.tabPageState.Text = "Goverment";
            // 
            // tabPageCity
            // 
            this.tabPageCity.Controls.Add(this.comboBoxCountry);
            this.tabPageCity.Controls.Add(this.labelCountryStats);
            this.tabPageCity.Controls.Add(this.comboBoxStates);
            this.tabPageCity.Controls.Add(this.labelStateStats);
            this.tabPageCity.Controls.Add(this.comboBoxCities);
            this.tabPageCity.Controls.Add(this.listBoxBuyOrders);
            this.tabPageCity.Controls.Add(this.listBoxSellOrders);
            this.tabPageCity.Controls.Add(this.listBoxCityStats);
            this.tabPageCity.Controls.Add(this.listBoxFactoryStats);
            this.tabPageCity.Location = new System.Drawing.Point(4, 22);
            this.tabPageCity.Name = "tabPageCity";
            this.tabPageCity.Size = new System.Drawing.Size(822, 454);
            this.tabPageCity.TabIndex = 2;
            this.tabPageCity.Text = "Economy";
            // 
            // tabPageDiplomacy
            // 
            this.tabPageDiplomacy.Controls.Add(this.labelProposedTrades);
            this.tabPageDiplomacy.Controls.Add(this.listBoxProposedTradeAgreements);
            this.tabPageDiplomacy.Controls.Add(this.buttonAcceptTrade);
            this.tabPageDiplomacy.Controls.Add(this.buttonRejectTrade);
            this.tabPageDiplomacy.Controls.Add(this.buttonProposeTrade);
            this.tabPageDiplomacy.Controls.Add(this.buttonViewRelations);
            this.tabPageDiplomacy.Controls.Add(this.buttonOpenTradeManagement);
            this.tabPageDiplomacy.Location = new System.Drawing.Point(4, 22);
            this.tabPageDiplomacy.Name = "tabPageDiplomacy";
            this.tabPageDiplomacy.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageDiplomacy.Size = new System.Drawing.Size(822, 454);
            this.tabPageDiplomacy.TabIndex = 3;
            this.tabPageDiplomacy.Text = "Diplomacy";
            this.tabPageDiplomacy.UseVisualStyleBackColor = true;
            // 
            // labelProposedTrades
            // 
            this.labelProposedTrades.AutoSize = true;
            this.labelProposedTrades.Location = new System.Drawing.Point(10, 190);
            this.labelProposedTrades.Name = "labelProposedTrades";
            this.labelProposedTrades.Size = new System.Drawing.Size(183, 13);
            this.labelProposedTrades.TabIndex = 0;
            this.labelProposedTrades.Text = "Proposed Trade Agreements (to you):";
            // 
            // listBoxProposedTradeAgreements
            // 
            this.listBoxProposedTradeAgreements.FormattingEnabled = true;
            this.listBoxProposedTradeAgreements.Location = new System.Drawing.Point(10, 210);
            this.listBoxProposedTradeAgreements.Name = "listBoxProposedTradeAgreements";
            this.listBoxProposedTradeAgreements.Size = new System.Drawing.Size(380, 147);
            this.listBoxProposedTradeAgreements.TabIndex = 1;
            // 
            // buttonAcceptTrade
            // 
            this.buttonAcceptTrade.Location = new System.Drawing.Point(10, 370);
            this.buttonAcceptTrade.Name = "buttonAcceptTrade";
            this.buttonAcceptTrade.Size = new System.Drawing.Size(90, 23);
            this.buttonAcceptTrade.TabIndex = 2;
            this.buttonAcceptTrade.Text = "Accept Trade";
            this.buttonAcceptTrade.UseVisualStyleBackColor = true;
            this.buttonAcceptTrade.Click += new System.EventHandler(this.ButtonAcceptTrade_Click);
            // 
            // buttonRejectTrade
            // 
            this.buttonRejectTrade.Location = new System.Drawing.Point(110, 370);
            this.buttonRejectTrade.Name = "buttonRejectTrade";
            this.buttonRejectTrade.Size = new System.Drawing.Size(90, 23);
            this.buttonRejectTrade.TabIndex = 3;
            this.buttonRejectTrade.Text = "Reject Trade";
            this.buttonRejectTrade.UseVisualStyleBackColor = true;
            this.buttonRejectTrade.Click += new System.EventHandler(this.ButtonRejectTrade_Click);
            // 
            // buttonProposeTrade
            // 
            this.buttonProposeTrade.Location = new System.Drawing.Point(450, 30);
            this.buttonProposeTrade.Name = "buttonProposeTrade";
            this.buttonProposeTrade.Size = new System.Drawing.Size(120, 23);
            this.buttonProposeTrade.TabIndex = 4;
            this.buttonProposeTrade.Text = "Propose New Trade";
            this.buttonProposeTrade.UseVisualStyleBackColor = true;
            this.buttonProposeTrade.Click += new System.EventHandler(this.ButtonProposeTrade_Click);
            // 
            // buttonViewRelations
            // 
            this.buttonViewRelations.Location = new System.Drawing.Point(450, 60);
            this.buttonViewRelations.Name = "buttonViewRelations";
            this.buttonViewRelations.Size = new System.Drawing.Size(120, 23);
            this.buttonViewRelations.TabIndex = 5;
            this.buttonViewRelations.Text = "View Relations";
            this.buttonViewRelations.UseVisualStyleBackColor = true;
            this.buttonViewRelations.Click += new System.EventHandler(this.ButtonViewRelations_Click);
            // 
            // buttonOpenTradeManagement
            // 
            this.buttonOpenTradeManagement.Location = new System.Drawing.Point(450, 90);
            this.buttonOpenTradeManagement.Name = "buttonOpenTradeManagement";
            this.buttonOpenTradeManagement.Size = new System.Drawing.Size(120, 23);
            this.buttonOpenTradeManagement.TabIndex = 6;
            this.buttonOpenTradeManagement.Text = "Trade Management";
            this.buttonOpenTradeManagement.UseVisualStyleBackColor = true;
            this.buttonOpenTradeManagement.Click += new System.EventHandler(this.ButtonOpenTradeManagement_Click);
            // 
            // tabPageDebug
            // 
            this.tabPageDebug.Controls.Add(this.buttonToggleDebug);
            this.tabPageDebug.Controls.Add(this.labelCurrentRole);
            this.tabPageDebug.Controls.Add(this.labelRoleType);
            this.tabPageDebug.Controls.Add(this.comboBoxRoleType);
            this.tabPageDebug.Controls.Add(this.labelEntitySelection);
            this.tabPageDebug.Controls.Add(this.comboBoxCountrySelection);
            this.tabPageDebug.Controls.Add(this.comboBoxStateSelection);
            this.tabPageDebug.Controls.Add(this.comboBoxCorporationSelection);
            this.tabPageDebug.Controls.Add(this.buttonAssumeRole);
            this.tabPageDebug.Controls.Add(this.buttonRelinquishRole);
            this.tabPageDebug.Controls.Add(this.checkBoxLogPops);
            this.tabPageDebug.Controls.Add(this.checkBoxLogBuildings);
            this.tabPageDebug.Controls.Add(this.checkBoxLogEconomy);
            this.tabPageDebug.Location = new System.Drawing.Point(4, 22);
            this.tabPageDebug.Name = "tabPageDebug";
            this.tabPageDebug.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageDebug.Size = new System.Drawing.Size(822, 454);
            this.tabPageDebug.TabIndex = 4;
            this.tabPageDebug.Text = "Debug";
            this.tabPageDebug.UseVisualStyleBackColor = true;
            // 
            // buttonToggleDebug
            // 
            this.buttonToggleDebug.Location = new System.Drawing.Point(10, 10);
            this.buttonToggleDebug.Name = "buttonToggleDebug";
            this.buttonToggleDebug.Size = new System.Drawing.Size(120, 23);
            this.buttonToggleDebug.TabIndex = 0;
            this.buttonToggleDebug.Text = "Toggle Debug";
            this.buttonToggleDebug.UseVisualStyleBackColor = true;
            this.buttonToggleDebug.Click += new System.EventHandler(this.ButtonToggleDebug_Click);
            // 
            // labelCurrentRole
            // 
            this.labelCurrentRole.AutoSize = true;
            this.labelCurrentRole.Location = new System.Drawing.Point(10, 50);
            this.labelCurrentRole.Name = "labelCurrentRole";
            this.labelCurrentRole.Size = new System.Drawing.Size(69, 13);
            this.labelCurrentRole.TabIndex = 1;
            this.labelCurrentRole.Text = "Current Role:";
            // 
            // labelRoleType
            // 
            this.labelRoleType.AutoSize = true;
            this.labelRoleType.Location = new System.Drawing.Point(10, 80);
            this.labelRoleType.Name = "labelRoleType";
            this.labelRoleType.Size = new System.Drawing.Size(59, 13);
            this.labelRoleType.TabIndex = 2;
            this.labelRoleType.Text = "Role Type:";
            // 
            // comboBoxRoleType
            // 
            this.comboBoxRoleType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxRoleType.FormattingEnabled = true;
            this.comboBoxRoleType.Location = new System.Drawing.Point(80, 80);
            this.comboBoxRoleType.Name = "comboBoxRoleType";
            this.comboBoxRoleType.Size = new System.Drawing.Size(200, 21);
            this.comboBoxRoleType.TabIndex = 3;
            // 
            // labelEntitySelection
            // 
            this.labelEntitySelection.AutoSize = true;
            this.labelEntitySelection.Location = new System.Drawing.Point(10, 110);
            this.labelEntitySelection.Name = "labelEntitySelection";
            this.labelEntitySelection.Size = new System.Drawing.Size(83, 13);
            this.labelEntitySelection.TabIndex = 4;
            this.labelEntitySelection.Text = "Entity Selection:";
            // 
            // comboBoxCountrySelection
            // 
            this.comboBoxCountrySelection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCountrySelection.FormattingEnabled = true;
            this.comboBoxCountrySelection.Location = new System.Drawing.Point(10, 130);
            this.comboBoxCountrySelection.Name = "comboBoxCountrySelection";
            this.comboBoxCountrySelection.Size = new System.Drawing.Size(200, 21);
            this.comboBoxCountrySelection.TabIndex = 5;
            // 
            // comboBoxStateSelection
            // 
            this.comboBoxStateSelection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxStateSelection.FormattingEnabled = true;
            this.comboBoxStateSelection.Location = new System.Drawing.Point(10, 160);
            this.comboBoxStateSelection.Name = "comboBoxStateSelection";
            this.comboBoxStateSelection.Size = new System.Drawing.Size(200, 21);
            this.comboBoxStateSelection.TabIndex = 6;
            // 
            // comboBoxCorporationSelection
            // 
            this.comboBoxCorporationSelection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxCorporationSelection.FormattingEnabled = true;
            this.comboBoxCorporationSelection.Location = new System.Drawing.Point(10, 190);
            this.comboBoxCorporationSelection.Name = "comboBoxCorporationSelection";
            this.comboBoxCorporationSelection.Size = new System.Drawing.Size(200, 21);
            this.comboBoxCorporationSelection.TabIndex = 7;
            // 
            // buttonAssumeRole
            // 
            this.buttonAssumeRole.Location = new System.Drawing.Point(10, 220);
            this.buttonAssumeRole.Name = "buttonAssumeRole";
            this.buttonAssumeRole.Size = new System.Drawing.Size(120, 23);
            this.buttonAssumeRole.TabIndex = 8;
            this.buttonAssumeRole.Text = "Assume Role";
            this.buttonAssumeRole.UseVisualStyleBackColor = true;
            this.buttonAssumeRole.Click += new System.EventHandler(this.ButtonAssumeRole_Click);
            // 
            // buttonRelinquishRole
            // 
            this.buttonRelinquishRole.Location = new System.Drawing.Point(10, 250);
            this.buttonRelinquishRole.Name = "buttonRelinquishRole";
            this.buttonRelinquishRole.Size = new System.Drawing.Size(120, 23);
            this.buttonRelinquishRole.TabIndex = 9;
            this.buttonRelinquishRole.Text = "Relinquish Role";
            this.buttonRelinquishRole.UseVisualStyleBackColor = true;
            this.buttonRelinquishRole.Click += new System.EventHandler(this.ButtonRelinquishRole_Click);
            // 
            // checkBoxLogPops
            // 
            this.checkBoxLogPops.AutoSize = true;
            this.checkBoxLogPops.Location = new System.Drawing.Point(150, 280);
            this.checkBoxLogPops.Name = "checkBoxLogPops";
            this.checkBoxLogPops.Size = new System.Drawing.Size(93, 17);
            this.checkBoxLogPops.TabIndex = 10;
            this.checkBoxLogPops.Text = "Log Pop Stats";
            this.checkBoxLogPops.UseVisualStyleBackColor = true;
            this.checkBoxLogPops.CheckedChanged += new System.EventHandler(this.CheckBoxLogPops_CheckedChanged);
            // 
            // checkBoxLogBuildings
            // 
            this.checkBoxLogBuildings.AutoSize = true;
            this.checkBoxLogBuildings.Location = new System.Drawing.Point(150, 300);
            this.checkBoxLogBuildings.Name = "checkBoxLogBuildings";
            this.checkBoxLogBuildings.Size = new System.Drawing.Size(111, 17);
            this.checkBoxLogBuildings.TabIndex = 11;
            this.checkBoxLogBuildings.Text = "Log Building Stats";
            this.checkBoxLogBuildings.UseVisualStyleBackColor = true;
            this.checkBoxLogBuildings.CheckedChanged += new System.EventHandler(this.CheckBoxLogBuildings_CheckedChanged);
            // 
            // checkBoxLogEconomy
            // 
            this.checkBoxLogEconomy.AutoSize = true;
            this.checkBoxLogEconomy.Location = new System.Drawing.Point(150, 320);
            this.checkBoxLogEconomy.Name = "checkBoxLogEconomy";
            this.checkBoxLogEconomy.Size = new System.Drawing.Size(118, 17);
            this.checkBoxLogEconomy.TabIndex = 12;
            this.checkBoxLogEconomy.Text = "Log Economy Stats";
            this.checkBoxLogEconomy.UseVisualStyleBackColor = true;
            this.checkBoxLogEconomy.CheckedChanged += new System.EventHandler(this.CheckBoxLogEconomy_CheckedChanged);
            // 
            // tabPageFinance
            // 
            this.tabPageFinance.Controls.Add(this.listViewFinance);
            this.tabPageFinance.Location = new System.Drawing.Point(4, 22);
            this.tabPageFinance.Name = "tabPageFinance";
            this.tabPageFinance.Size = new System.Drawing.Size(822, 454);
            this.tabPageFinance.TabIndex = 5;
            this.tabPageFinance.Text = "Finance";
            this.tabPageFinance.UseVisualStyleBackColor = true;
            // 
            // listViewFinance
            // 
            this.listViewFinance.FullRowSelect = true;
            this.listViewFinance.GridLines = true;
            this.listViewFinance.HideSelection = false;
            this.listViewFinance.Location = new System.Drawing.Point(10, 10);
            this.listViewFinance.Name = "listViewFinance";
            this.listViewFinance.Size = new System.Drawing.Size(800, 430);
            this.listViewFinance.TabIndex = 0;
            this.listViewFinance.UseCompatibleStateImageBehavior = false;
            this.listViewFinance.View = System.Windows.Forms.View.Details;
            // 
            // pictureBox1
            // 
            this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(822, 454);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // MainGame
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(927, 641);
            this.Controls.Add(this.tabControlMain);
            this.Controls.Add(this.labelSimTime);
            this.Controls.Add(this.listBoxMarketStats);
            this.Name = "MainGame";
            this.Text = "MainGame";
            this.tabControlMain.ResumeLayout(false);
            this.tabPageCountry.ResumeLayout(false);
            this.tabPageCity.ResumeLayout(false);
            this.tabPageDiplomacy.ResumeLayout(false);
            this.tabPageDiplomacy.PerformLayout();
            this.tabPageDebug.ResumeLayout(false);
            this.tabPageDebug.PerformLayout();
            this.tabPageFinance.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
    }
}