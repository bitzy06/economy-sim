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
            this.tabPageDiplomacy = new System.Windows.Forms.TabPage(); // Added
            this.tabPageDebug = new System.Windows.Forms.TabPage(); // Debug tab for role switching
            this.tabPageFinance = new System.Windows.Forms.TabPage();
            this.listViewFinance = new System.Windows.Forms.ListView();
            // this.listBoxActiveTradeAgreements = new System.Windows.Forms.ListBox(); // REMOVED by commenting
            this.listBoxProposedTradeAgreements = new System.Windows.Forms.ListBox(); // Added
            this.buttonAcceptTrade = new System.Windows.Forms.Button(); // Added
            this.buttonRejectTrade = new System.Windows.Forms.Button(); // Added
            this.buttonProposeTrade = new System.Windows.Forms.Button(); // Added
            this.buttonViewRelations = new System.Windows.Forms.Button(); // Added
            this.buttonOpenTradeManagement = new System.Windows.Forms.Button(); // Added for enhanced trade system
            // this.labelActiveTrades = new System.Windows.Forms.Label(); // REMOVED by commenting
            this.labelProposedTrades = new System.Windows.Forms.Label(); // Added
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
            this.tabControlMain.SuspendLayout();
            this.tabPageCity.SuspendLayout();
            this.tabPageDiplomacy.SuspendLayout(); // Added
            this.tabPageDebug.SuspendLayout(); // Debug tab for role switching
            this.tabPageFinance.SuspendLayout();
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
            this.tabControlMain.Controls.Add(this.tabPageDiplomacy); // Added
            this.tabControlMain.Controls.Add(this.tabPageDebug); // Debug tab for role switching
            this.tabControlMain.Controls.Add(this.tabPageFinance);
            this.tabControlMain.Location = new System.Drawing.Point(10, 10);
            this.tabControlMain.Name = "tabControlMain";
            this.tabControlMain.SelectedIndex = 0;
            this.tabControlMain.Size = new System.Drawing.Size(830, 480);
            this.tabControlMain.TabIndex = 0;
            // 
            // tabPageCountry
            // 
            this.tabPageCountry.Location = new System.Drawing.Point(4, 22);
            this.tabPageCountry.Name = "tabPageCountry";
            this.tabPageCountry.Size = new System.Drawing.Size(822, 454);
            this.tabPageCountry.TabIndex = 0;
            this.tabPageCountry.Text = "Country";
            // 
            // tabPageState
            // 
            this.tabPageState.Location = new System.Drawing.Point(4, 22);
            this.tabPageState.Name = "tabPageState";
            this.tabPageState.Size = new System.Drawing.Size(822, 454);
            this.tabPageState.TabIndex = 1;
            this.tabPageState.Text = "State";
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
            this.tabPageCity.Text = "City";
            // 
            // tabPageDiplomacy
            // 
            // this.tabPageDiplomacy.Controls.Add(this.labelActiveTrades); // REMOVED by commenting
            // this.tabPageDiplomacy.Controls.Add(this.listBoxActiveTradeAgreements); // REMOVED by commenting
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
            this.tabPageDiplomacy.TabIndex = 3; // Adjusted TabIndex
            this.tabPageDiplomacy.Text = "Diplomacy";
            this.tabPageDiplomacy.UseVisualStyleBackColor = true;
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
            this.tabPageDebug.Location = new System.Drawing.Point(4, 22);
            this.tabPageDebug.Name = "tabPageDebug";
            this.tabPageDebug.Padding = new System.Windows.Forms.Padding(3);
            this.tabPageDebug.Size = new System.Drawing.Size(822, 454);
            this.tabPageDebug.TabIndex = 4; // Next TabIndex
            this.tabPageDebug.Text = "Debug";
            this.tabPageDebug.UseVisualStyleBackColor = true;
            // 
            // tabPageFinance
            // 
            this.tabPageFinance.Location = new System.Drawing.Point(4, 22);
            this.tabPageFinance.Name = "tabPageFinance";
            this.tabPageFinance.Size = new System.Drawing.Size(822, 454);
            this.tabPageFinance.TabIndex = 5;
            this.tabPageFinance.Text = "Finance";
            this.tabPageFinance.UseVisualStyleBackColor = true;
            this.tabPageFinance.Controls.Add(this.listViewFinance);
            // 
            // listViewFinance
            // 
            this.listViewFinance.Location = new System.Drawing.Point(10, 10);
            this.listViewFinance.Name = "listViewFinance";
            this.listViewFinance.Size = new System.Drawing.Size(800, 430);
            this.listViewFinance.TabIndex = 0;
            this.listViewFinance.View = System.Windows.Forms.View.Details;
            this.listViewFinance.FullRowSelect = true;
            this.listViewFinance.GridLines = true;
            // 
            // labelActiveTrades
            // 
            /* // REMOVED by commenting
            this.labelActiveTrades.AutoSize = true;
            this.labelActiveTrades.Location = new System.Drawing.Point(10, 10);
            this.labelActiveTrades.Name = "labelActiveTrades";
            this.labelActiveTrades.Size = new System.Drawing.Size(100, 13);
            this.labelActiveTrades.Text = "Active Trade Agreements:";
            */
            // 
            // listBoxActiveTradeAgreements
            // 
            /* // REMOVED by commenting
            this.listBoxActiveTradeAgreements.FormattingEnabled = true;
            this.listBoxActiveTradeAgreements.Location = new System.Drawing.Point(10, 30);
            this.listBoxActiveTradeAgreements.Name = "listBoxActiveTradeAgreements";
            this.listBoxActiveTradeAgreements.Size = new System.Drawing.Size(380, 150);
            this.listBoxActiveTradeAgreements.TabIndex = 0;
            */
            // 
            // labelProposedTrades
            // 
            this.labelProposedTrades.AutoSize = true;
            this.labelProposedTrades.Location = new System.Drawing.Point(10, 190);
            this.labelProposedTrades.Name = "labelProposedTrades";
            this.labelProposedTrades.Size = new System.Drawing.Size(120, 13);
            this.labelProposedTrades.Text = "Proposed Trade Agreements (to you):";
            // 
            // listBoxProposedTradeAgreements
            // 
            this.listBoxProposedTradeAgreements.FormattingEnabled = true;
            this.listBoxProposedTradeAgreements.Location = new System.Drawing.Point(10, 210);
            this.listBoxProposedTradeAgreements.Name = "listBoxProposedTradeAgreements";
            this.listBoxProposedTradeAgreements.Size = new System.Drawing.Size(380, 150);
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
            this.labelCurrentRole.Size = new System.Drawing.Size(70, 13);
            this.labelCurrentRole.TabIndex = 1;
            this.labelCurrentRole.Text = "Current Role:";
            // 
            // labelRoleType
            // 
            this.labelRoleType.AutoSize = true;
            this.labelRoleType.Location = new System.Drawing.Point(10, 80);
            this.labelRoleType.Name = "labelRoleType";
            this.labelRoleType.Size = new System.Drawing.Size(60, 13);
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
            this.labelEntitySelection.Size = new System.Drawing.Size(85, 13);
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
            this.tabPageCity.ResumeLayout(false);
            this.tabPageDiplomacy.ResumeLayout(false); // Added
            this.tabPageDiplomacy.PerformLayout(); // Added for labels
            this.tabPageDebug.ResumeLayout(false); // Debug tab for role switching
            this.tabPageDebug.PerformLayout(); // Added for labels
            this.tabPageFinance.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
    }
}