using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using StrategyGame; // For game classes like Country, State, City, PopClass, Factory, Good, etc. AND DTOs
using System.Text.RegularExpressions; // Added for owner-drawing
using System.IO; // For File operations
using System.Text.Json; // For JSON deserialization

namespace economy_sim
{
    public partial class MainGame : Form
    {
        private List<StrategyGame.City> allCitiesInWorld; // Renamed from 'cities' for clarity, will hold all cities from all countries
        private List<StrategyGame.Country> allCountries; // Explicitly StrategyGame.Country
        // The 'country' field might now represent the currently selected/player country 
        // Or be removed if all interaction is through allCountries list and UI selection
        private StrategyGame.Country playerCountry; // Explicitly StrategyGame.Country
        private int simTurn = 0;
        private Button buttonShowPopStats;
        private PopStatsForm popStatsForm;
        private FactoryStatsForm factoryStatsForm;
        private List<State> states;
        private PlayerRoleManager playerRoleManager;
        private Random random = new Random(); // Add a Random instance for AI and other uses
        private StrategyGame.DiplomacyManager diplomacyManager; // Added DiplomacyManager field
        private ListView listViewDiplomacy; // Added ListView for diplomacy

        // Fields to store previous values for change tracking
        private Dictionary<string, double> prevCityMetrics = new Dictionary<string, double>();
        private Dictionary<string, int> prevFactoryWorkers = new Dictionary<string, int>(); // Key: Factory Name
        private Dictionary<string, double> prevMarketPrices = new Dictionary<string, double>(); // Key: Good Name
        private Dictionary<string, int> prevMarketSupply = new Dictionary<string, int>();   // Key: Good Name
        private Dictionary<string, int> prevMarketDemand = new Dictionary<string, int>();   // Key: Good Name
        private double prevStateBudget;
        private double prevCountryBudget;
        private bool firstTick = true; // To avoid showing changes on the very first display

        // Fields for the enhanced trade systems from trade.cs
        private TradeRouteManager tradeRouteManager;
        private EnhancedTradeManager enhancedTradeManager;
        private StrategyGame.GlobalMarket globalMarket; // Ensured namespace qualification

        private bool isDetailedDebugMode = false; // Flag to track the current debug mode

        public MainGame()
        {
            InitializeComponent();
            // Removed call to non-existent InitializeDebugControls()
            playerRoleManager = new PlayerRoleManager();
            allCitiesInWorld = new List<StrategyGame.City>();
            allCountries = new List<StrategyGame.Country>();
            states = new List<StrategyGame.State>();

            // Adjust listBoxMarketStats height
            if (this.listBoxMarketStats != null) 
            {
                this.listBoxMarketStats.Height += 70; 
            }

            // Ensure event handlers for designer controls are attached
            comboBoxStates.SelectedIndexChanged += ComboBoxStates_SelectedIndexChanged;
            comboBoxCities.SelectedIndexChanged += ComboBoxCities_SelectedIndexChanged;
            comboBoxCountry.SelectedIndexChanged += ComboBoxCountry_SelectedIndexChanged;

            InitializeGameData();
            
            // Initialize DiplomacyManager after allCountries is populated
            diplomacyManager = new StrategyGame.DiplomacyManager(allCountries);
            
            // Initialize enhanced trade systems
            tradeRouteManager = new TradeRouteManager();
            enhancedTradeManager = new EnhancedTradeManager(allCountries);
            globalMarket = new StrategyGame.GlobalMarket(); // Ensured namespace qualification

            // Initialize listViewDiplomacy
            listViewDiplomacy = new ListView
            {
                // Dock = DockStyle.Fill, // Changed from Fill
                Location = new System.Drawing.Point(10, 10),
                Size = new System.Drawing.Size(400, 180), // Adjusted size
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };

            listViewDiplomacy.Columns.Add("Country", 120);
            listViewDiplomacy.Columns.Add("Type", 80);
            listViewDiplomacy.Columns.Add("Resource", 100);
            listViewDiplomacy.Columns.Add("Quantity", 80);
            listViewDiplomacy.Columns.Add("Price", 80);
            listViewDiplomacy.Columns.Add("Remaining", 80);

            tabPageDiplomacy.Controls.Add(listViewDiplomacy);

            // Initialize corporations and assign roles after game data is loaded
            InitializeCorporations();

            // Initialize the Debug tab
            InitializeDebugTab();
            
            // Initialize and populate Finance tab
            InitializeFinanceTab();
            UpdateFinanceTab();

            UpdateOrderLists();
            timerSim.Tick += TimerSim_Tick;
            timerSim.Start();
            
            int buttonsTargetX = 30;
            int buttonsTargetY = 411;

            // Initialize and position buttons
            this.buttonShowPopStats = new Button();
            this.buttonShowPopStats.Text = "Show Pop Stats";
            this.buttonShowPopStats.Location = new System.Drawing.Point(buttonsTargetX, buttonsTargetY);
            this.buttonShowPopStats.Size = new System.Drawing.Size(120, 23);
            this.buttonShowPopStats.Click += ButtonShowPopStats_Click;
            if (this.tabControlMain.TabPages.ContainsKey("tabPageCity"))
            {
                 this.tabControlMain.TabPages["tabPageCity"].Controls.Add(this.buttonShowPopStats);
                 this.buttonShowPopStats.BringToFront(); // Ensure it's on top
            }
            else if (this.tabPageCity != null) // Fallback if tabPageCity is a direct field
            {
                this.tabPageCity.Controls.Add(this.buttonShowPopStats);
                this.buttonShowPopStats.BringToFront(); // Ensure it's on top
            }
            
            popStatsForm = new PopStatsForm();
            factoryStatsForm = new FactoryStatsForm(); 
            tabControlMain.SelectedIndexChanged += TabControlMain_SelectedIndexChanged;

            // Setup ListBoxes for owner-drawing
            this.listBoxCityStats.DrawMode = DrawMode.OwnerDrawFixed;
            this.listBoxFactoryStats.DrawMode = DrawMode.OwnerDrawFixed;
            this.listBoxMarketStats.DrawMode = DrawMode.OwnerDrawFixed;
            this.listBoxCityStats.DrawItem += new DrawItemEventHandler(this.ListBox_DrawItemShared);
            this.listBoxFactoryStats.DrawItem += new DrawItemEventHandler(this.ListBox_DrawItemShared);
            this.listBoxMarketStats.DrawItem += new DrawItemEventHandler(this.ListBox_DrawItemShared);

            // Instantiate and position buttonShowFactoryStats (local variable for this constructor scope)
            Button buttonShowFactoryStats = new Button();
            buttonShowFactoryStats.Text = "Building Details";
            buttonShowFactoryStats.Location = new System.Drawing.Point(this.buttonShowPopStats.Right + 10, buttonsTargetY); 
            buttonShowFactoryStats.Size = new System.Drawing.Size(120, 23);
            buttonShowFactoryStats.Click += ButtonShowFactoryStats_Click;
            if (this.tabControlMain.TabPages.ContainsKey("tabPageCity"))
            {
                this.tabControlMain.TabPages["tabPageCity"].Controls.Add(buttonShowFactoryStats);
                buttonShowFactoryStats.BringToFront(); // Ensure it's on top
            }
            else if (this.tabPageCity != null)
            {
                 this.tabPageCity.Controls.Add(buttonShowFactoryStats);
                 buttonShowFactoryStats.BringToFront(); // Ensure it's on top
            }
            
            // Example: Assign player a starting role for testing
            if (playerCountry != null && states.Any() && allCitiesInWorld.Any())
            {
                // Create player's corporation
                Corporation playerCorp = new Corporation("PlayerCorp Global");
                Market.AllCorporations.Add(playerCorp);
                playerRoleManager.AssumeRoleCEO(playerCorp);

                // Create AI Corporations with Specializations
                List<Corporation> aiCorps = new List<Corporation>();
                aiCorps.Add(new Corporation("General Industries Inc.", CorporationSpecialization.HeavyIndustry));
                aiCorps.Add(new Corporation("Resource Group Ltd.", CorporationSpecialization.Mining));
                aiCorps.Add(new Corporation("AgriCorp International", CorporationSpecialization.Agriculture));
                aiCorps.Add(new Corporation("Everyday Goods Co.", CorporationSpecialization.LightIndustry));
                
                foreach (var corp in aiCorps)
                {
                    Market.AllCorporations.Add(corp);
                }

                // Assign factory ownership
                bool playerCorpHasFactory = false;
                int currentAiCorpIndex = 0;

                foreach (var cityToProcess in allCitiesInWorld) // Iterate through all cities in the 'allCitiesInWorld' list
                {
                    foreach (var factory in cityToProcess.Factories)
                    {
                        if (!playerCorpHasFactory && factory.Name == "Grain Farm" && cityToProcess.Name == "Metro City") // Assign a specific factory to player
                        {
                            factory.OwnerCorporation = playerCorp;
                            playerCorp.AddFactory(factory);
                            playerCorpHasFactory = true;
                        }
                        else
                        {
                            if (aiCorps.Any()) // Ensure there are AI corps to assign to
                            {
                                Corporation assignedCorp = aiCorps[currentAiCorpIndex];
                                factory.OwnerCorporation = assignedCorp;
                                assignedCorp.AddFactory(factory);
                                currentAiCorpIndex = (currentAiCorpIndex + 1) % aiCorps.Count;
                            }
                            else
                            {
                                // Handle case where there are no AI corps (e.g., assign to city/state, or leave unowned for now)
                                Console.WriteLine($"Warning: No AI corporations to assign factory {factory.Name} in {cityToProcess.Name}");
                            }
                        }
                    }
                }
                
                // If playerCorp still doesn't have a factory (e.g. specific one not found), assign the very first one encountered.
                if (!playerCorpHasFactory && allCitiesInWorld.Any() && allCitiesInWorld.First().Factories.Any())
                {
                    var firstCity = allCitiesInWorld.First();
                    var firstFactoryInList = firstCity.Factories.First();
                    // Check if it's already owned by an AI corp from the loop above due to logic change
                    if (firstFactoryInList.OwnerCorporation == null || !aiCorps.Contains(firstFactoryInList.OwnerCorporation)) 
                    { 
                         // If previously assigned player factory was not found, and this one is unassigned or not AI owned, assign it
                        if(firstFactoryInList.OwnerCorporation != null && firstFactoryInList.OwnerCorporation != playerCorp) {
                           firstFactoryInList.OwnerCorporation.OwnedFactories.Remove(firstFactoryInList); // Remove from previous temp owner if any
                        }
                        firstFactoryInList.OwnerCorporation = playerCorp;
                        playerCorp.AddFactory(firstFactoryInList);
                        Console.WriteLine($"Assigned fallback factory {firstFactoryInList.Name} to PlayerCorp Global");
                    }
                }
            }
        }

        private void InitializeGameData()
        {
            // 1. Clear all global static lists first
            Market.GoodDefinitions.Clear();
            Market.AllCorporations.Clear(); 
            FactoryBlueprints.AllBlueprints.Clear(); 
            allCitiesInWorld.Clear();
            allCountries.Clear();
            playerCountry = null;
            comboBoxCountry.Items.Clear();
            comboBoxStates.Items.Clear(); 
            comboBoxCities.Items.Clear(); 

            // 2. Initialize Factory Blueprints (this also populates Market.GoodDefinitions now)
            FactoryBlueprints.InitializeBlueprints(); 

            // 3. Load World Setup from JSON
            string jsonFilePath = "world_setup.json";
            StrategyGame.WorldSetupData worldData = null;
            if (File.Exists(jsonFilePath))
            {
                try
                {
                    string jsonData = File.ReadAllText(jsonFilePath);
                    worldData = System.Text.Json.JsonSerializer.Deserialize<StrategyGame.WorldSetupData>(jsonData, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading world_setup.json: {ex.Message}", "World Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    CreateDefaultFallbackWorld(); 
                    return;
                }
            }
            else
            {
                MessageBox.Show($"world_setup.json not found. Creating a default world.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                CreateDefaultFallbackWorld();
                return;
            }

            if (worldData == null || worldData.Countries == null || !worldData.Countries.Any())
            {
                MessageBox.Show("No country data found in world_setup.json or data is invalid. Creating a default world.", "Invalid Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                CreateDefaultFallbackWorld();
                return;
            }

            // 4. Create Game Objects from Loaded Data
            foreach (StrategyGame.CountryData countryData in worldData.Countries)
            {
                StrategyGame.Country currentCountry = new StrategyGame.Country(countryData.Name);
                // Set up tax policies instead of a single TaxRate
                currentCountry.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.IncomeTax, (decimal)countryData.TaxRate));
                currentCountry.NationalExpenses = countryData.NationalExpenses;
                currentCountry.Budget = countryData.InitialBudget;
                currentCountry.Population = countryData.InitialPopulation; 

                if (countryData.States != null)
                {
                    foreach (StrategyGame.StateData stateData in countryData.States)
                    {
                        StrategyGame.State currentState = new StrategyGame.State(stateData.Name);
                        currentState.TaxRate = stateData.TaxRate;
                        currentState.StateExpenses = stateData.StateExpenses;
                        currentState.Budget = stateData.InitialBudget;
                        currentState.Population = stateData.InitialPopulation;

                        if (stateData.Cities != null)
                        {
                            foreach (StrategyGame.CityData cityData in stateData.Cities)
                            {
                                StrategyGame.City currentCity = new StrategyGame.City(cityData.Name);
                                currentCity.Population = cityData.InitialPopulation;
                                currentCity.Budget = cityData.InitialBudget;
                                currentCity.TaxRate = cityData.TaxRate;
                                currentCity.CityExpenses = cityData.CityExpenses;
                                // City constructor handles PopClasses and initial market dicts.

                                if (cityData.InitialFactories != null)
                                {
                                    foreach (StrategyGame.InitialFactoryData factoryData in cityData.InitialFactories)
                                    {
                                        StrategyGame.FactoryBlueprint blueprint = StrategyGame.FactoryBlueprints.AllBlueprints.FirstOrDefault(bp => bp.FactoryTypeName == factoryData.FactoryTypeName);
                                        if (blueprint != null)
                                        {
                                            StrategyGame.Factory newFactory = new StrategyGame.Factory(factoryData.FactoryTypeName, factoryData.Capacity);
                                            newFactory.OutputGoods.Add(new StrategyGame.Good(blueprint.OutputGood.Name, blueprint.OutputGood.BasePrice, blueprint.OutputGood.Category, blueprint.OutputGood.Quantity));
                                            foreach (var inputGoodBlueprint in blueprint.InputGoods)
                                            {
                                                newFactory.InputGoods.Add(new StrategyGame.Good(inputGoodBlueprint.Name, inputGoodBlueprint.BasePrice, inputGoodBlueprint.Category, inputGoodBlueprint.Quantity));
                                            }
                                            int totalSlots = factoryData.Capacity * 10; 
                                            newFactory.JobSlots.Clear(); // Ensure clean slate for job slots
                                            if (blueprint.DefaultJobSlotDistribution != null) {
                                                foreach(var jobDist in blueprint.DefaultJobSlotDistribution)
                                                {
                                                    newFactory.JobSlots[jobDist.Key] = (int)(totalSlots * jobDist.Value);
                                                }
                                                // Ensure all slots assigned if rounding issues (simple assignment)
                                                int assignedSlots = newFactory.JobSlots.Values.Sum();
                                                if (assignedSlots < totalSlots && newFactory.JobSlots.Any()) {
                                                    newFactory.JobSlots[newFactory.JobSlots.First().Key] += (totalSlots - assignedSlots);
                                                } else if (assignedSlots < totalSlots) {
                                                    newFactory.JobSlots["Laborers"] = totalSlots; // Default if empty
                                                }
                                            }
                                            currentCity.Factories.Add(newFactory);
                                        }
                                        else
                                        {
                                            Console.WriteLine($"Warning: FactoryBlueprint '{factoryData.FactoryTypeName}' not found for city {currentCity.Name}.");
                                        }
                                    }
                                }
                                currentState.Cities.Add(currentCity);
                                allCitiesInWorld.Add(currentCity);
                            }
                        }
                        // Recalculate state population/budget from cities if specified as 0 in JSON, or for verification
                        if (currentState.Population == 0 && currentState.Cities.Any()) currentState.Population = currentState.Cities.Sum(c => c.Population);
                        // Budget might be an initial grant rather than sum
                        currentCountry.States.Add(currentState);
                    }
                }
                // Recalculate country population/budget from states if specified as 0 in JSON
                if (currentCountry.Population == 0 && currentCountry.States.Any()) currentCountry.Population = currentCountry.States.Sum(s => s.Population);
                
                allCountries.Add(currentCountry);
                comboBoxCountry.Items.Add(currentCountry.Name);

                if (countryData.IsPlayerControlled)
                {
                    if (playerCountry != null) { Console.WriteLine("Warning: Multiple player-controlled countries defined. Using the first one encountered."); }
                    else { playerCountry = currentCountry; }
                }
            }
            
            if (playerCountry == null && allCountries.Any())
            {
                playerCountry = allCountries.First();
                Console.WriteLine($"No player-controlled country explicitly set in JSON, defaulting to: {playerCountry.Name}");
            }

            // Initialize DiplomacyManager after countries are loaded
            diplomacyManager = new StrategyGame.DiplomacyManager(allCountries);

            // Update UI selection after all countries are loaded
            if (allCountries.Any() && comboBoxCountry.Items.Count > 0)
            {
                 if (playerCountry != null && comboBoxCountry.Items.Contains(playerCountry.Name)) 
                 {
                    comboBoxCountry.SelectedItem = playerCountry.Name;
                 }
                 else if(comboBoxCountry.Items.Count > 0) 
            {
                comboBoxCountry.SelectedIndex = 0;
            }
            }
            // The rest of InitializeGameData from Part 3a already handles SelectedIndex = 0 if no playerCountry

            UpdateCountryStats(); 
            UpdateStateStats(); 
            UpdateCityAndFactoryStats(); 
            UpdateMarketStats(); 

            var initialCity = GetSelectedCity();
            if (initialCity != null)
            {
                StrategyGame.Economy.UpdateCityEconomy(initialCity); // Initial pass to generate orders/needs
            }
        }

        private void CreateDefaultFallbackWorld()
        {
            Console.WriteLine("Creating a default fallback world as world_setup.json was not found or was invalid.");
            // Ensure FactoryBlueprints and Market.GoodDefinitions are populated (should be from InitializeGameData call)
            if (!FactoryBlueprints.AllBlueprints.Any() || !Market.GoodDefinitions.Any())
            {
                FactoryBlueprints.InitializeBlueprints(); // Ensure critical data is loaded
            }

            StrategyGame.Country defaultCountry = new StrategyGame.Country("Default Nation");
            // Set up default tax policy
            defaultCountry.FinancialSystem.AddTaxPolicy(new TaxPolicy(TaxType.IncomeTax, 0.1m));
            defaultCountry.NationalExpenses = 10000;
            defaultCountry.Budget = 500000;
            defaultCountry.Population = 100000; // Simplified total population

            StrategyGame.State defaultState = new StrategyGame.State("Default Province");
            defaultState.TaxRate = 0.08;
            defaultState.StateExpenses = 2000;
            defaultState.Budget = defaultCountry.Budget * 0.1; // Example budget portion
            defaultState.Population = defaultCountry.Population; // Simplified state population
            defaultCountry.States.Add(defaultState);

            StrategyGame.City defaultCity = new StrategyGame.City("Capital City");
            defaultCity.Population = defaultState.Population; // Simplified city population
            defaultCity.Budget = defaultState.Budget * 0.2; // Example budget portion
            defaultCity.TaxRate = 0.05;
            defaultCity.CityExpenses = 1000;
            
            // Add a basic farm to the default city
            StrategyGame.FactoryBlueprint farmBlueprint = StrategyGame.FactoryBlueprints.AllBlueprints.FirstOrDefault(bp => bp.FactoryTypeName == "Grain Farm");
            if (farmBlueprint != null)
            {
                StrategyGame.Factory farm = new StrategyGame.Factory(farmBlueprint.FactoryTypeName, 1); // Capacity 1
                farm.OutputGoods.Add(new StrategyGame.Good(farmBlueprint.OutputGood.Name, farmBlueprint.OutputGood.BasePrice, farmBlueprint.OutputGood.Category, farmBlueprint.OutputGood.Quantity));
                foreach (var inputGoodBlueprint in farmBlueprint.InputGoods)
                {
                    farm.InputGoods.Add(new StrategyGame.Good(inputGoodBlueprint.Name, inputGoodBlueprint.BasePrice, inputGoodBlueprint.Category, inputGoodBlueprint.Quantity));
                }
                int totalSlots = 1 * 10; 
                farm.JobSlots.Clear();
                if (farmBlueprint.DefaultJobSlotDistribution != null) {
                    foreach(var jobDist in farmBlueprint.DefaultJobSlotDistribution)
                    {
                        farm.JobSlots[jobDist.Key] = (int)(totalSlots * jobDist.Value);
                    }
                    int assignedSlots = farm.JobSlots.Values.Sum();
                    if (assignedSlots < totalSlots && farm.JobSlots.Any()) {
                        farm.JobSlots[farm.JobSlots.First().Key] += (totalSlots - assignedSlots);
                    } else if (assignedSlots < totalSlots) {
                         farm.JobSlots["Laborers"] = totalSlots;
                    }
                }
                defaultCity.Factories.Add(farm);
            }
            defaultState.Cities.Add(defaultCity);
            allCitiesInWorld.Add(defaultCity);

            allCountries.Add(defaultCountry);
            comboBoxCountry.Items.Add(defaultCountry.Name);
            playerCountry = defaultCountry; // Assign this as the player country
            // UI should be updated after this returns to InitializeGameData by the existing logic there
            
            InitializeCorporationsAndAssignFactories(); // Critical to call this for fallback world too
        }

        private void InitializeCorporationsAndAssignFactories()
        {
            Console.WriteLine("Initializing corporations and assigning factory ownership...");
            Market.AllCorporations.Clear(); // Clear any previous corporations

            if (!allCountries.Any() || !allCitiesInWorld.Any()) 
            {
                Console.WriteLine("No world data (countries/cities) to initialize corporations for.");
                return;
            }

            StrategyGame.Corporation playerCorp = null;
            if (playerCountry != null) 
            {
                playerCorp = new StrategyGame.Corporation($"{playerCountry.Name} Holdings Inc."); 
                playerCorp.IsPlayerControlled = true; 
                Market.AllCorporations.Add(playerCorp);
                // playerRoleManager.AssumeRoleCEO(playerCorp); // Assuming PlayerRoleManager is setup
                 if (playerRoleManager != null) playerRoleManager.AssumeRoleCEO(playerCorp);
            }
            else
            {
                Console.WriteLine("No player country defined, cannot create player corporation.");
            }

            List<StrategyGame.Corporation> aiCorps = new List<StrategyGame.Corporation>();
            aiCorps.Add(new StrategyGame.Corporation("Aethel Mining Union", CorporationSpecialization.Mining));
            aiCorps.Add(new StrategyGame.Corporation("Borland Agricultural Combine", CorporationSpecialization.Agriculture));
            aiCorps.Add(new StrategyGame.Corporation("Ceria Heavy Industries", CorporationSpecialization.HeavyIndustry));
            aiCorps.Add(new StrategyGame.Corporation("Global Consumer Goods Ltd.", CorporationSpecialization.LightIndustry));
            aiCorps.Add(new StrategyGame.Corporation("Trans-National Logistics", CorporationSpecialization.Diversified));
            
            foreach (var corp in aiCorps)
            {
                Market.AllCorporations.Add(corp);
            }

                int currentAiCorpIndex = 0;
            foreach (var city in allCitiesInWorld)
            {
                foreach (var factory in city.Factories)
                {
                    if (factory.OwnerCorporation == null) 
                    {
                        bool assignedToPlayer = false;
                        // Simple heuristic: if it's the player's country and player has few/no factories, give them some starter ones.
                        if (playerCorp != null && city.Name.Contains(playerCountry.States.FirstOrDefault()?.Cities.FirstOrDefault()?.Name ?? "") && playerCorp.OwnedFactories.Count < 2) 
                        {
                            Country factorysCountry = allCountries.FirstOrDefault(co => co.States.Any(s => s.Cities.Contains(city)));
                            if (factorysCountry == playerCountry)
                        {
                            factory.OwnerCorporation = playerCorp;
                            playerCorp.AddFactory(factory);
                                assignedToPlayer = true;
                                Console.WriteLine($"Assigned factory '{factory.Name}' in {city.Name} to Player Corp: {playerCorp.Name}");
                        }
                        }

                        if (!assignedToPlayer && aiCorps.Any())
                        {
                            StrategyGame.Corporation assignedCorp = aiCorps[currentAiCorpIndex];
                                factory.OwnerCorporation = assignedCorp;
                                assignedCorp.AddFactory(factory);
                            // Console.WriteLine($"Assigned factory '{factory.Name}' in {city.Name} to AI Corp: {assignedCorp.Name}");
                                currentAiCorpIndex = (currentAiCorpIndex + 1) % aiCorps.Count;
                            }
                        else if (!assignedToPlayer && playerCorp != null) // Fallback if no AI corps but player corp exists
                        {
                             factory.OwnerCorporation = playerCorp;
                             playerCorp.AddFactory(factory);
                             Console.WriteLine($"Fallback: Assigned factory '{factory.Name}' in {city.Name} to Player Corp: {playerCorp.Name}");
                        }
                    }
                }
            }
            if (playerCorp != null && !playerCorp.OwnedFactories.Any() && allCitiesInWorld.Any(c => c.Factories.Any()))
            {
                 Console.WriteLine("Warning: Player corporation ('{playerCorp.Name}') still has no factories. Review assignment logic or initial factory data in JSON.");
            }
            Console.WriteLine($"Total corporations initialized: {Market.AllCorporations.Count}");
        }

        private void InitializeCorporations()
        {
            if (playerCountry != null)
            {
                // Create player's corporation
                Corporation playerCorp = new Corporation("PlayerCorp Global");
                Market.AllCorporations.Add(playerCorp);
                playerRoleManager.AssumeRoleCEO(playerCorp);

                // Create AI Corporations with Specializations
                List<Corporation> aiCorps = new List<Corporation>();
                aiCorps.Add(new Corporation("General Industries Inc.", CorporationSpecialization.HeavyIndustry));
                aiCorps.Add(new Corporation("Resource Group Ltd.", CorporationSpecialization.Mining));
                aiCorps.Add(new Corporation("AgriCorp International", CorporationSpecialization.Agriculture));
                aiCorps.Add(new Corporation("Everyday Goods Co.", CorporationSpecialization.LightIndustry));
                
                foreach (var corp in aiCorps)
                {
                    Market.AllCorporations.Add(corp);
                }

                // Assign factory ownership
                bool playerCorpHasFactory = false;
                int currentAiCorpIndex = 0;

                foreach (var cityToProcess in allCitiesInWorld)
                {
                    foreach (var factory in cityToProcess.Factories)
                    {
                        if (!playerCorpHasFactory && factory.Name == "Grain Farm" && cityToProcess.Name == "Metro City")
                        {
                            factory.OwnerCorporation = playerCorp;
                            playerCorp.AddFactory(factory);
                            playerCorpHasFactory = true;
                        }
                        else
                        {
                            if (aiCorps.Any())
                            {
                                Corporation assignedCorp = aiCorps[currentAiCorpIndex];
                                factory.OwnerCorporation = assignedCorp;
                                assignedCorp.AddFactory(factory);
                                currentAiCorpIndex = (currentAiCorpIndex + 1) % aiCorps.Count;
                            }
                            else
                            {
                                Console.WriteLine($"Warning: No AI corporations to assign factory {factory.Name} in {cityToProcess.Name}");
                            }
                        }
                    }
                }
                
                // If playerCorp still doesn't have a factory, assign the first available one
                if (!playerCorpHasFactory && allCitiesInWorld.Any() && allCitiesInWorld.First().Factories.Any())
                {
                    var firstCity = allCitiesInWorld.First();
                    var firstFactoryInList = firstCity.Factories.First();
                    if (firstFactoryInList.OwnerCorporation == null || !aiCorps.Contains(firstFactoryInList.OwnerCorporation))
                    {
                        if (firstFactoryInList.OwnerCorporation != null && firstFactoryInList.OwnerCorporation != playerCorp)
                        {
                            firstFactoryInList.OwnerCorporation.OwnedFactories.Remove(firstFactoryInList);
                        }
                        firstFactoryInList.OwnerCorporation = playerCorp;
                        playerCorp.AddFactory(firstFactoryInList);
                        Console.WriteLine($"Assigned fallback factory {firstFactoryInList.Name} to PlayerCorp Global");
                    }
                }
            }
        }

        private StrategyGame.City GetSelectedCity()
        {
            if (comboBoxCities.SelectedItem == null) return null;
            string cityName = comboBoxCities.SelectedItem.ToString();
            // This needs to search through the selected state's cities, or allCitiesInWorld if flat list is used for UI
            State selectedState = GetSelectedState();
            if (selectedState != null)
            {
                return selectedState.Cities.FirstOrDefault(c => c.Name == cityName);
            }
            // Fallback if state selection is not robust yet
            return allCitiesInWorld.FirstOrDefault(c => c.Name == cityName); 
        }

        private State GetSelectedState()
        {
            if (comboBoxStates.SelectedItem == null || comboBoxCountry.SelectedItem == null) return null;
            string stateName = comboBoxStates.SelectedItem.ToString();
            string countryName = comboBoxCountry.SelectedItem.ToString();
            StrategyGame.Country selectedCountry = allCountries.FirstOrDefault(co => co.Name == countryName);
            if (selectedCountry != null)
            {
                return selectedCountry.States.FirstOrDefault(s => s.Name == stateName);
            }
            return null;
        }

        private void ComboBoxCities_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            UpdateCityAndFactoryStats();
            UpdateMarketStats();
            UpdateOrderLists();
            if (factoryStatsForm != null && factoryStatsForm.Visible) // Update factory stats if form is visible and city changes
            {
                factoryStatsForm.UpdateStats(GetSelectedCity());
            }
        }

        private void TimerSim_Tick(object sender, EventArgs e)
        {
            simTurn++;
            labelSimTime.Text = $"Turn: {simTurn}";

            // Get the currently selected city for UI before any updates this tick
            var cityCurrentlySelectedForUI = GetSelectedCity();

            // 1. Capture Previous Stats for the Selected City (if any, and not the first tick)
            if (cityCurrentlySelectedForUI != null && !firstTick)
            {
                prevCityMetrics["Population"] = cityCurrentlySelectedForUI.Population;
                prevCityMetrics["Budget"] = cityCurrentlySelectedForUI.Budget;
                prevCityMetrics["Happiness"] = cityCurrentlySelectedForUI.Happiness;

                prevFactoryWorkers.Clear();
                foreach (var factory in cityCurrentlySelectedForUI.Factories)
                {
                    // Ensure factory name is unique enough if multiple cities can have same factory names
                    // For now, assuming factory names are unique within a city or globally for prevFactoryWorkers keying
                    prevFactoryWorkers[factory.Name] = factory.WorkersEmployed;
                }
                
                prevMarketPrices.Clear(); 
                prevMarketSupply.Clear(); 
                prevMarketDemand.Clear();
                if (cityCurrentlySelectedForUI.LocalPrices != null) // Ensure dictionary exists
                {
                    foreach (var goodName in cityCurrentlySelectedForUI.LocalPrices.Keys)
                    {
                        prevMarketPrices[goodName] = cityCurrentlySelectedForUI.LocalPrices.ContainsKey(goodName) ? cityCurrentlySelectedForUI.LocalPrices[goodName] : 0;
                        prevMarketSupply[goodName] = cityCurrentlySelectedForUI.LocalSupply.ContainsKey(goodName) ? cityCurrentlySelectedForUI.LocalSupply[goodName] : 0;
                        prevMarketDemand[goodName] = cityCurrentlySelectedForUI.LocalDemand.ContainsKey(goodName) ? cityCurrentlySelectedForUI.LocalDemand[goodName] : 0;
                    }
                }
                var selectedState = GetSelectedState(); // If state/country budgets also have indicators
                if (selectedState != null) { prevStateBudget = selectedState.Budget; }
                if (playerCountry != null) { prevCountryBudget = playerCountry.Budget; }
            }

            // 2. --- Corporation AI Update Phase ---
            if (Market.AllCorporations != null && allCitiesInWorld != null && FactoryBlueprints.AllBlueprints.Any()) 
            {
                List<Good> goodPrototypes = Market.GoodDefinitions.Values.ToList(); 
                foreach (var corp in Market.AllCorporations)
                {
                    if (!corp.IsPlayerControlled)
                    {
                        corp.UpdateAI(this.allCitiesInWorld, goodPrototypes, random);
                    }
                }
            }
            // --- End Corporation AI Update Phase ---

            // 3. --- City Economies Update Phase ---
            if (allCitiesInWorld != null)
            {
                foreach (var city in allCitiesInWorld) 
                {
                    Market.ResetCitySupplyDemand(city); 
                    foreach (var factory in city.Factories)
                    {
                        factory.Produce(city.Stockpile, city); 
                    }
                    StrategyGame.Economy.UpdateCityEconomy(city); // Populates ImportNeeds and ExportableSurplus
                    Market.UpdateCityPrices(city); 
                }
            }
            // --- End City Economies Update Phase ---

            // --- Inter-City Trade Resolution Phase ---
            if (allCitiesInWorld != null && allCitiesInWorld.Count > 1)
            {
                Market.ResolveInterCityTrade(this.allCitiesInWorld, 0.1); // Using a base trade cost of 0.1 for now
            }
            // --- End Inter-City Trade Resolution Phase ---
            
            // --- Trade Routes and Global Market Update Phase ---
            if (tradeRouteManager != null)
            {
                tradeRouteManager.UpdateAllRoutes();
            }
            
            if (enhancedTradeManager != null)
            {
                enhancedTradeManager.ProcessTurnEnd();
            }
            
            if (globalMarket != null && allCountries != null && allCitiesInWorld != null)
            {
                globalMarket.UpdateGlobalMarket(allCitiesInWorld, allCountries, tradeRouteManager, enhancedTradeManager);
            }
            // --- End Trade Routes and Global Market Update Phase ---

            // Process global market updates
            if (globalMarket != null)
            {
                globalMarket.UpdateGlobalMarket(allCitiesInWorld, allCountries, tradeRouteManager, enhancedTradeManager);
                
                // Generate some sample trade data for demonstration
                if (playerCountry != null && allCountries.Count > 1)
                {
                    // Get a couple of random countries for trade examples
                    var tradingCountries = allCountries.Where(c => c != playerCountry).OrderBy(x => random.Next()).Take(3).ToList();
                    
                    if (tradingCountries.Any())
                    {
                        // Generate some sample trade data for common goods
                        string[] sampleGoods = { "Grain", "Coal", "Iron", "Steel", "Fabric", "Lumber", "Oil", "Electronics", "Machinery" };
                        
                        foreach (var good in sampleGoods)
                        {
                            // Player country exports to another country (player is the producer)
                            if (random.Next(100) < 30) // 30% chance
                            {
                                var tradingPartner = tradingCountries[random.Next(tradingCountries.Count)];
                                int quantity = random.Next(20, 500);
                                double pricePerUnit = random.Next(5, 50);
                                double totalValue = quantity * pricePerUnit;
                                
                                // The names are correct - player exports, partner imports
                                globalMarket.RecordTrade(good, playerCountry.Name, tradingPartner.Name, quantity, totalValue);
                            }
                            
                            // Player country imports from another country (player is the consumer)
                            if (random.Next(100) < 30) // 30% chance
                            {
                                var tradingPartner = tradingCountries[random.Next(tradingCountries.Count)];
                                int quantity = random.Next(20, 500);
                                double pricePerUnit = random.Next(5, 50);
                                double totalValue = quantity * pricePerUnit;
                                
                                // The names are correct - partner exports, player imports
                                globalMarket.RecordTrade(good, tradingPartner.Name, playerCountry.Name, quantity, totalValue);
                            }
                            
                            // AI countries trade with each other
                            if (tradingCountries.Count >= 2 && random.Next(100) < 40) // 40% chance
                            {
                                var exporter = tradingCountries[random.Next(tradingCountries.Count)];
                                var importer = tradingCountries.Where(c => c != exporter).OrderBy(x => random.Next()).FirstOrDefault();
                                
                                if (importer != null)
                                {
                                    int quantity = random.Next(50, 1000);
                                    double pricePerUnit = random.Next(5, 50);
                                    double totalValue = quantity * pricePerUnit;
                                    
                                    globalMarket.RecordTrade(good, exporter.Name, importer.Name, quantity, totalValue);
                                }
                            }
                        }
                        
                        // Add some specialized trade - certain countries specialize in certain goods
                        if (tradingCountries.Count >= 3)
                        {
                            // First country specializes in luxury goods
                            var luxuryExporter = tradingCountries[0];
                            string[] luxuryGoods = { "Jewelry", "Wine", "Art", "Perfume" };
                            
                            foreach (var good in luxuryGoods)
                            {
                                if (random.Next(100) < 70) // 70% chance - they're specialists
                                {
                                    // Export to player
                                    int quantity = random.Next(5, 50); // Lower quantity for luxury
                                    double pricePerUnit = random.Next(100, 1000); // Higher price
                                    double totalValue = quantity * pricePerUnit;
                                    
                                    globalMarket.RecordTrade(good, luxuryExporter.Name, playerCountry.Name, quantity, totalValue);
                                    
                                    // Also export to other AI countries
                                    foreach (var otherCountry in tradingCountries.Where(c => c != luxuryExporter))
                                    {
                                        if (random.Next(100) < 40)
                                        {
                                            quantity = random.Next(5, 50);
                                            pricePerUnit = random.Next(100, 1000);
                                            totalValue = quantity * pricePerUnit;
                                            
                                            globalMarket.RecordTrade(good, luxuryExporter.Name, otherCountry.Name, quantity, totalValue);
                                        }
                                    }
                                }
                            }
                            
                            // Second country specializes in industrial goods
                            var industrialExporter = tradingCountries[1];
                            string[] industrialGoods = { "Steel", "Machinery", "Chemicals", "Tools" };
                            
                            foreach (var good in industrialGoods)
                            {
                                if (random.Next(100) < 60) // 60% chance
                                {
                                    // Export to player
                                    int quantity = random.Next(200, 1000);
                                    double pricePerUnit = random.Next(20, 100);
                                    double totalValue = quantity * pricePerUnit;
                                    
                                    globalMarket.RecordTrade(good, industrialExporter.Name, playerCountry.Name, quantity, totalValue);
                                }
                            }
                            
                            // Third country specializes in raw materials
                            var rawMaterialsExporter = tradingCountries[2];
                            string[] rawMaterials = { "Iron", "Coal", "Oil", "Timber", "Minerals" };
                            
                            foreach (var good in rawMaterials)
                            {
                                if (random.Next(100) < 65) // 65% chance
                                {
                                    // Export to various countries including player
                                    int quantity = random.Next(500, 2000);
                                    double pricePerUnit = random.Next(10, 30);
                                    double totalValue = quantity * pricePerUnit;
                                    
                                    // Sometimes player exports these too
                                    if (random.Next(100) < 40)
                                    {
                                        globalMarket.RecordTrade(good, rawMaterialsExporter.Name, playerCountry.Name, quantity, totalValue);
                                    }
                                    else
                                    {
                                        globalMarket.RecordTrade(good, playerCountry.Name, rawMaterialsExporter.Name, quantity, totalValue);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Update state and country level economies before refreshing the UI
            if (allCountries != null)
            {
                foreach (var country in allCountries)
                {
                    foreach (var state in country.States)
                    {
                        StrategyGame.Economy.UpdateStateEconomy(state);
                    }
                    StrategyGame.Economy.UpdateCountryEconomy(country);
                }
            }

            // 4. Refresh UI elements
            // The GetSelectedCity() here will get the same city as cityCurrentlySelectedForUI,
            // but its data has now been updated by the simulation loop.
            UpdateOrderLists(); 
            UpdateCityAndFactoryStats(); 
            UpdateMarketStats(); 
            UpdateStateStats();
            UpdateCountryStats();

            if (cityCurrentlySelectedForUI != null) // Use the city selected at start of tick for populating forms
            { 
                if (popStatsForm != null && popStatsForm.Visible)
                {
                    popStatsForm.UpdateStats(cityCurrentlySelectedForUI); // Pass the (now updated) selected city
                }
                if (factoryStatsForm != null && factoryStatsForm.Visible) 
                {
                    factoryStatsForm.UpdateStats(cityCurrentlySelectedForUI); // Pass the (now updated) selected city
                }
            }
            firstTick = false;

            // Process end-of-turn for diplomacy
            if (diplomacyManager != null)
            {
                diplomacyManager.ProcessTurnEnd();
                UpdateDiplomacyTab(); // Update the diplomacy UI after processing
            }
            
            // Process financial systems and monetary effects
            foreach (var country in allCountries)
            {
                country.FinancialSystem.SimulateMonetaryEffects();
            }

            // Refresh finance tab if it's visible so data stays current
            if (tabControlMain.SelectedTab == tabPageFinance)
            {
                UpdateFinanceTab();
            }

            // Process AI trade proposals (temporary simple logic)
            if (diplomacyManager != null && playerCountry != null && random.Next(100) < 20) // 20% chance each turn
            {
                // Get a random country that isn't the player
                var otherCountry = allCountries.Where(c => c != playerCountry).OrderBy(x => random.Next()).FirstOrDefault();
                if (otherCountry != null)
                {
                    // Simple example: AI proposes to sell a random resource
                    var availableResources = otherCountry.Resources.Where(r => r.Value > 0).ToList();
                    if (availableResources.Any())
                    {
                        var randomResource = availableResources[random.Next(availableResources.Count)];
                        var quantity = Math.Min(randomResource.Value * 0.1, random.Next(10, 100)); // 10% of available or random amount
                        var price = random.Next(50, 200); // Random price between 50 and 200

                        var newTrade = new StrategyGame.TradeAgreement(
                            otherCountry.Name,
                            playerCountry.Name,
                            randomResource.Key,
                            quantity,
                            price,
                            random.Next(3, 8) // Random duration between 3-7 turns
                        );

                        diplomacyManager.ProposeTradeAgreement(newTrade);
                    }
                }
            }
        }
        
        private string FormatValueWithChange(double currentValue, double previousValue, string formatSpecifier, bool calculateDiff, double tolerance = 0.001)
        {
            string valueStr = string.Format($"{{0:{formatSpecifier}}}", currentValue);
            if (calculateDiff && prevCityMetrics.Count > 0) // Ensure previous values are populated
            {
                double diff = currentValue - previousValue;
                if (Math.Abs(diff) > tolerance)
                {
                    string sign = diff > 0 ? "+" : "";
                    valueStr += $" ({sign}{string.Format($"{{0:{formatSpecifier}}}", diff)})";
                }
            }
            return valueStr;
        }

        private string FormatValueWithChange(int currentValue, int previousValue, string formatSpecifier, bool calculateDiff, int tolerance = 0)
        {
            string valueStr = string.Format($"{{0:{formatSpecifier}}}", currentValue);
            if (calculateDiff && prevCityMetrics.Count > 0) // Ensure previous values are populated (using prevCityMetrics as a general proxy for populated state)
            {
                int diff = currentValue - previousValue;
                if (Math.Abs(diff) > tolerance) 
                {
                    string sign = diff > 0 ? "+" : "";
                    valueStr += $" ({sign}{string.Format($"{{0:{formatSpecifier}}}", diff)})";
                }
            }
            return valueStr;
        }

        private void UpdateCityAndFactoryStats()
        {
            var city = GetSelectedCity();
            if (city == null) return;

            int prevCityStatsTopIndex = 0;
            if (listBoxCityStats.Items.Count > 0) prevCityStatsTopIndex = listBoxCityStats.TopIndex;
            int prevFactoryStatsTopIndex = 0;
            if (listBoxFactoryStats.Items.Count > 0) prevFactoryStatsTopIndex = listBoxFactoryStats.TopIndex;

            listBoxCityStats.BeginUpdate();
            listBoxFactoryStats.BeginUpdate();

            listBoxCityStats.Items.Clear();

            listBoxCityStats.Items.Add($"Population: {FormatValueWithChange(city.Population, prevCityMetrics.TryGetValue("Population", out double prevPop) ? (int)prevPop : city.Population, "N0", !firstTick)}");
            listBoxCityStats.Items.Add($"Budget: {FormatValueWithChange(city.Budget, prevCityMetrics.TryGetValue("Budget", out double prevBudget) ? prevBudget : city.Budget, "C", !firstTick)}");
            listBoxCityStats.Items.Add($"Tax Rate: {city.TaxRate * 100}%");
            listBoxCityStats.Items.Add($"Expenses: {city.CityExpenses}");
            listBoxCityStats.Items.Add($"Happiness: {FormatValueWithChange(city.Happiness, prevCityMetrics.TryGetValue("Happiness", out double prevHappy) ? (int)prevHappy : city.Happiness, "", !firstTick)}");

            // Display some key LOCAL market prices within city stats, with change indicators
            if (city.LocalPrices.ContainsKey("Grain"))
            {
                listBoxCityStats.Items.Add($"Grain Price (Local): {FormatValueWithChange(city.LocalPrices["Grain"], prevMarketPrices.TryGetValue("Grain", out double prevGP) ? prevGP : city.LocalPrices["Grain"], "0.00", !firstTick)}");
            }
            if (city.LocalPrices.ContainsKey("Cloth"))
            {
                listBoxCityStats.Items.Add($"Cloth Price (Local): {FormatValueWithChange(city.LocalPrices["Cloth"], prevMarketPrices.TryGetValue("Cloth", out double prevCP) ? prevCP : city.LocalPrices["Cloth"], "0.00", !firstTick)}");
            }
            
            listBoxFactoryStats.Items.Clear();
            foreach (var factory in city.Factories)
            {
                string inputs = string.Join(", ", factory.InputGoods.Select(g => $"{g.Name} x{g.Quantity}"));
                string outputs = string.Join(", ", factory.OutputGoods.Select(g => $"{g.Name} x{g.Quantity}"));
                string workersStr = FormatValueWithChange(factory.WorkersEmployed, prevFactoryWorkers.TryGetValue(factory.Name, out int prevWorkers) ? prevWorkers : factory.WorkersEmployed, "", !firstTick);
                listBoxFactoryStats.Items.Add($"{factory.Name} | Cap: {factory.ProductionCapacity} | Workers: {workersStr}");
                listBoxFactoryStats.Items.Add($"  Inputs: {inputs}");
                listBoxFactoryStats.Items.Add($"  Outputs: {outputs}");
            }

            listBoxCityStats.EndUpdate();
            listBoxFactoryStats.EndUpdate();

            if (prevCityStatsTopIndex >= 0 && prevCityStatsTopIndex < listBoxCityStats.Items.Count) listBoxCityStats.TopIndex = prevCityStatsTopIndex;
            else if (listBoxCityStats.Items.Count > 0) listBoxCityStats.TopIndex = 0;

            if (prevFactoryStatsTopIndex >= 0 && prevFactoryStatsTopIndex < listBoxFactoryStats.Items.Count) listBoxFactoryStats.TopIndex = prevFactoryStatsTopIndex;
            else if (listBoxFactoryStats.Items.Count > 0) listBoxFactoryStats.TopIndex = 0;
        }

        private void UpdateMarketStats() // Now displays LOCAL market for selected city
        {
            var city = GetSelectedCity();
            if (city == null) 
            {
                listBoxMarketStats.Items.Clear();
                listBoxMarketStats.Items.Add("No city selected to display local market.");
                return;
            }

            int prevMarketStatsTopIndex = 0;
            if (listBoxMarketStats.Items.Count > 0) prevMarketStatsTopIndex = listBoxMarketStats.TopIndex;
            
            listBoxMarketStats.BeginUpdate();
            listBoxMarketStats.Items.Clear();
            listBoxMarketStats.Items.Add($"--- Local Market: {city.Name} ---");
            
            // Use city.LocalPrices, city.LocalSupply, city.LocalDemand
            foreach (var goodName in city.LocalPrices.Keys.ToList()) 
            {
                double currentPrice = city.LocalPrices[goodName];
                int currentSupply = city.LocalSupply.ContainsKey(goodName) ? city.LocalSupply[goodName] : 0;
                int currentDemand = city.LocalDemand.ContainsKey(goodName) ? city.LocalDemand[goodName] : 0;

                string priceStr = FormatValueWithChange(currentPrice, prevMarketPrices.TryGetValue(goodName, out double prevP) ? prevP : currentPrice, "0.00", !firstTick);
                string supplyStr = FormatValueWithChange(currentSupply, prevMarketSupply.TryGetValue(goodName, out int prevS) ? prevS : currentSupply, "N0", !firstTick);
                string demandStr = FormatValueWithChange(currentDemand, prevMarketDemand.TryGetValue(goodName, out int prevD) ? prevD : currentDemand, "N0", !firstTick);
                                
                string line = $"{goodName}: Price {priceStr} | Supply {supplyStr} | Demand {demandStr}";
                listBoxMarketStats.Items.Add(line);
            }
            listBoxMarketStats.EndUpdate();

            if (prevMarketStatsTopIndex >= 0 && prevMarketStatsTopIndex < listBoxMarketStats.Items.Count) listBoxMarketStats.TopIndex = prevMarketStatsTopIndex;
            else if (listBoxMarketStats.Items.Count > 0) listBoxMarketStats.TopIndex = 0;
        }

        private void ButtonShowPopStats_Click(object sender, EventArgs e)
        {
            var city = GetSelectedCity();
            if (city != null)
            {
                popStatsForm.UpdateStats(city);
                popStatsForm.Show();
                popStatsForm.BringToFront();
            }
        }

        private void ButtonShowFactoryStats_Click(object sender, EventArgs e)
        {
            var city = GetSelectedCity();
            if (city != null)
            {
                factoryStatsForm.UpdateStats(city);
                factoryStatsForm.Show();
                factoryStatsForm.BringToFront();
            }
        }

        private void UpdateOrderLists()
        {
            var city = GetSelectedCity();

            int prevBuyOrdersTopIndex = 0;
            if (listBoxBuyOrders.Items.Count > 0) prevBuyOrdersTopIndex = listBoxBuyOrders.TopIndex;
            int prevSellOrdersTopIndex = 0;
            if (listBoxSellOrders.Items.Count > 0) prevSellOrdersTopIndex = listBoxSellOrders.TopIndex;

            listBoxBuyOrders.BeginUpdate();
            listBoxSellOrders.BeginUpdate();

            listBoxBuyOrders.Items.Clear();
            listBoxSellOrders.Items.Clear();
            if (city == null) 
            {
                listBoxBuyOrders.EndUpdate();
                listBoxSellOrders.EndUpdate();
                return;
            }
            listBoxBuyOrders.Items.Add("--- Buy Orders (Pop Classes) ---");
            foreach (var order in city.BuyOrders)
            {
                listBoxBuyOrders.Items.Add($"{order.Buyer.Name}: {order.Good} x{order.Quantity} @ max {order.MaxPrice:0.00}");
            }
            listBoxSellOrders.Items.Add("--- Sell Orders (Factories) ---");
            foreach (var order in city.SellOrders)
            {
                listBoxSellOrders.Items.Add($"{order.Seller.Name}: {order.Good} x{order.Quantity} @ min {order.MinPrice:0.00}");
            }

            listBoxBuyOrders.EndUpdate();
            listBoxSellOrders.EndUpdate();

            if (prevBuyOrdersTopIndex >= 0 && prevBuyOrdersTopIndex < listBoxBuyOrders.Items.Count) listBoxBuyOrders.TopIndex = prevBuyOrdersTopIndex;
            else if (listBoxBuyOrders.Items.Count > 0) listBoxBuyOrders.TopIndex = 0;

            if (prevSellOrdersTopIndex >= 0 && prevSellOrdersTopIndex < listBoxSellOrders.Items.Count) listBoxSellOrders.TopIndex = prevSellOrdersTopIndex;
            else if (listBoxSellOrders.Items.Count > 0) listBoxSellOrders.TopIndex = 0;
        }

        private void ComboBoxStates_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            UpdateCityComboBox();
            UpdateStateStats();
            // When state changes, the city might change, so update city-dependent UI too
            UpdateCityAndFactoryStats();
            UpdateMarketStats();
            UpdateOrderLists();
        }

        private void UpdateCityComboBox()
        {
            comboBoxCities.Items.Clear();
            if (comboBoxStates.SelectedIndex < 0) return;
            var state = states[comboBoxStates.SelectedIndex];
            comboBoxCities.Items.AddRange(state.Cities.Select(c => c.Name).ToArray());
            if (comboBoxCities.Items.Count > 0)
                comboBoxCities.SelectedIndex = 0;
        }

        private void UpdateStateStats()
        {
            if (comboBoxStates.SelectedIndex >= 0)
            {
                var selectedState = states[comboBoxStates.SelectedIndex];
                string budgetStr = FormatValueWithChange(selectedState.Budget, prevStateBudget, "C", !firstTick, 0.01);
                labelStateStats.Text = $"State Budget: {budgetStr}, Tax Rate: {selectedState.TaxRate:P}, Expenses: {selectedState.StateExpenses:C}";
            }
            else
            {
                labelStateStats.Text = "No state selected.";
            }
        }

        private void UpdateCountryStats()
        {
            if (playerCountry != null && comboBoxCountry.SelectedIndex >= 0) 
            {
                string budgetStr = FormatValueWithChange(playerCountry.Budget, prevCountryBudget, "C", !firstTick, 0.01);
                // Display tax policies instead of a single TaxRate
                var incomeTaxPolicy = playerCountry.FinancialSystem.TaxPolicies.FirstOrDefault(tp => tp.Type == TaxType.IncomeTax);
                string taxRateStr = incomeTaxPolicy.Id != Guid.Empty ? $"{incomeTaxPolicy.Rate:P}" : "N/A";
                labelCountryStats.Text = $"Country: {playerCountry.Name}, Budget: {budgetStr}, Tax Rate: {taxRateStr}, Expenses: {playerCountry.NationalExpenses:C}";
            }
            else
            {
                labelCountryStats.Text = "No country selected/loaded.";
            }
        }
        
        // Add a handler for country ComboBox selection change if you need to update UI based on it
        private void ComboBoxCountry_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (comboBoxCountry.SelectedItem == null) return;

            // Get the selected country
            string countryName = comboBoxCountry.SelectedItem.ToString();
            var selectedCountry = allCountries.FirstOrDefault(c => c.Name == countryName);
            
            if (selectedCountry != null)
            {
                // Update states ComboBox
                comboBoxStates.Items.Clear();
                states.Clear();  // Clear the states list
                states.AddRange(selectedCountry.States);  // Add the new country's states
                comboBoxStates.Items.AddRange(selectedCountry.States.Select(s => s.Name).ToArray());
                
                if (comboBoxStates.Items.Count > 0)
                {
                    comboBoxStates.SelectedIndex = 0;  // This will trigger ComboBoxStates_SelectedIndexChanged
                }
            }

            UpdateCountryStats();
        }

        private void TabControlMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Ensure the correct controls are visible and populated when a tab is selected
            // With selectors now on City tab, this logic might simplify.
            // Updates are mainly driven by ComboBox changes now.
            // However, forcing a refresh ensures diffs are calculated against the latest "previous" state if tab is switched.
            
            // No matter which tab, if a city is selected, all stats should be current.
            // The TimerSim_Tick handles continuous updates. This handles tab switching.
            // If firstTick is true, it means no simulation tick has completed fully to populate prev values for diffs.
            // So, we call the update methods to display initial state. If firstTick is false, they will calc diffs.

            UpdateCountryStats();
            UpdateStateStats(); 
            UpdateCityComboBox(); 
            UpdateCityAndFactoryStats();
            UpdateMarketStats(); 
            UpdateOrderLists();
            // If Finance tab selected, refresh finance data
            if (tabControlMain.SelectedTab == tabPageFinance)
            {
                UpdateFinanceTab();
            }

            // Debug and diplomacy handled similarly
            
        }

        private void ListBox_DrawItemShared(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            ListBox lb = (ListBox)sender;
            string itemText = lb.Items[e.Index].ToString();

            // Draw background (handles selection)
            e.DrawBackground();

            Font itemFont = e.Font;
            // Default text color (handles selection text color)
            Color defaultTextColor = (e.State & DrawItemState.Selected) == DrawItemState.Selected ? SystemColors.HighlightText : e.ForeColor;
            
            // Regex to find the main part and the change part (e.g., " (+50)" or " (-0.25)")
            // Group 1: Main text part (e.g., "Population: 1,050")
            // Group 2: The whole change part with leading space and parentheses (e.g., " (+50)")
            // Group 3: Just the content inside parentheses (e.g., "(+50)")
            var regex = new Regex(@"^(.*?)(\s*(\([+-][^)]+\)))?$");
            var match = regex.Match(itemText);

            string mainTextPart = match.Groups[1].Success ? match.Groups[1].Value : itemText; // Fallback to full itemText if regex fails unexpectedly
            string changeTextWithSpaceAndParens = string.Empty;
            string contentInsideParens = string.Empty;
            
            if (match.Success && match.Groups[2].Success && !string.IsNullOrEmpty(match.Groups[2].Value))
            {
                changeTextWithSpaceAndParens = match.Groups[2].Value;
                contentInsideParens = match.Groups[3].Value;
            }

            // Use the full item bounds for drawing, TextRenderer will handle clipping if needed.
            Rectangle itemDrawBounds = e.Bounds;
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.NoPadding; // Removed VerticalCenter

            // Draw main text part
            Size mainTextSize = TextRenderer.MeasureText(e.Graphics, mainTextPart, itemFont, itemDrawBounds.Size, flags);
            Rectangle mainTextRect = new Rectangle(itemDrawBounds.Left, itemDrawBounds.Top, mainTextSize.Width, itemDrawBounds.Height);
            TextRenderer.DrawText(e.Graphics, mainTextPart, itemFont, mainTextRect, defaultTextColor, flags);
            
            if (!string.IsNullOrEmpty(changeTextWithSpaceAndParens))
            {
                Color changeColor = defaultTextColor; 
                if (contentInsideParens.Contains("+"))
                    changeColor = Color.Green;
                else if (contentInsideParens.Contains("-"))
                    changeColor = Color.Red;

                Size changeTextSize = TextRenderer.MeasureText(e.Graphics, changeTextWithSpaceAndParens, itemFont, itemDrawBounds.Size, flags);
                Rectangle changeTextRect = new Rectangle(itemDrawBounds.Left + mainTextSize.Width, itemDrawBounds.Top, changeTextSize.Width, itemDrawBounds.Height);
                TextRenderer.DrawText(e.Graphics, changeTextWithSpaceAndParens, itemFont, changeTextRect, changeColor, flags);
            }

            // Draw focus rectangle if the item has focus
            e.DrawFocusRectangle();
        }

        private void UpdateDiplomacyTab()
        {
            // Clear existing items
            listViewDiplomacy.Items.Clear();

            if (playerCountry == null || diplomacyManager == null) return;

            // Add all active trade agreements
            var trades = diplomacyManager.GetTradeAgreementsForCountry(playerCountry.Name);
            foreach (var trade in trades)
            {
                var item = new ListViewItem(new string[] {
                    trade.FromCountryName == playerCountry.Name ? trade.ToCountryName : trade.FromCountryName,
                    trade.FromCountryName == playerCountry.Name ? "Export" : "Import",
                    trade.ResourceName,
                    trade.Quantity.ToString(),
                    trade.PricePerUnit.ToString("C"),
                    trade.TurnsRemaining.ToString()
                });
                listViewDiplomacy.Items.Add(item);
            }
        }

        private void ButtonProposeTrade_Click(object sender, EventArgs e)
        {
            if (playerCountry == null || diplomacyManager == null) return;
            
            using (var proposalForm = new TradeProposalForm(playerCountry, allCountries, diplomacyManager))
            {
                if (proposalForm.ShowDialog() == DialogResult.OK)
                {
                    UpdateDiplomacyTab();
                }
            }
        }

        private void ButtonViewRelations_Click(object sender, EventArgs e)
        {
            if (playerCountry == null || diplomacyManager == null) return;

            using (var relationsForm = new DiplomaticRelationsForm(playerCountry, allCountries, diplomacyManager))
            {
                relationsForm.ShowDialog();
            }
        }

        private void ButtonAcceptTrade_Click(object sender, EventArgs e)
        {
            if (listBoxProposedTradeAgreements.SelectedItem is TradeListItem selectedItem)
            {
                diplomacyManager.AcceptTradeAgreement(selectedItem.Trade.Id, playerCountry.Name);
                UpdateDiplomacyTab();
            }
        }

        private void ButtonRejectTrade_Click(object sender, EventArgs e)
        {
            if (listBoxProposedTradeAgreements.SelectedItem is TradeListItem selectedItem)
            {
                diplomacyManager.RejectTradeAgreement(selectedItem.Trade.Id, playerCountry.Name);
                UpdateDiplomacyTab();
            }
        }

        // Helper class for storing trade agreement references in the ListBox
        private class TradeListItem
        {
            public StrategyGame.TradeAgreement Trade { get; private set; }
            private string DisplayText { get; set; }

            public TradeListItem(StrategyGame.TradeAgreement trade, string displayText)
            {
                Trade = trade;
                DisplayText = displayText;
            }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        // Initialize Debug tab UI
        private void InitializeDebugTab()
        {
            // Debug logging toggle button
            Button buttonToggleDebug = new Button
            {
                Location = new Point(20, 20),
                Size = new Size(150, 30),
                Text = "Toggle Debug Logging: ON"
            };
            buttonToggleDebug.Click += (s, e) => {
                bool isEnabled = DebugLogger.ToggleLogging();
                buttonToggleDebug.Text = $"Toggle Debug Logging: {(isEnabled ? "ON" : "OFF")}";
                DebugLogger.Log($"Debug logging has been {(isEnabled ? "enabled" : "disabled")}");
            };
            tabPageDebug.Controls.Add(buttonToggleDebug);

            // Current role status (moved down)
            labelCurrentRole = new Label
            {
                Location = new Point(20, 60),
                AutoSize = true,
                Text = "Current Role: None"
            };
            tabPageDebug.Controls.Add(labelCurrentRole);
            
            // Role type selection
            Label labelRoleType = new Label
            {
                Location = new Point(20, 100),
                AutoSize = true,
                Text = "Select Role:"
            };
            tabPageDebug.Controls.Add(labelRoleType);
            
            comboBoxRoleType = new ComboBox
            {
                Location = new Point(120, 100),
                Size = new Size(200, 21),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            comboBoxRoleType.Items.AddRange(new object[] { "Prime Minister", "Governor", "CEO" });
            comboBoxRoleType.SelectedIndexChanged += ComboBoxRoleType_SelectedIndexChanged;
            tabPageDebug.Controls.Add(comboBoxRoleType);
            
            // Entity selection (country, state, corporation)
            labelEntitySelection = new Label
            {
                Location = new Point(20, 100),
                AutoSize = true,
                Text = "Select Entity:"
            };
            tabPageDebug.Controls.Add(labelEntitySelection);
            
            // Country selection for Prime Minister role
            comboBoxCountrySelection = new ComboBox
            {
                Location = new Point(120, 100),
                Size = new Size(200, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false // Initially hidden
            };
            tabPageDebug.Controls.Add(comboBoxCountrySelection);
            
            // State selection for Governor role
            comboBoxStateSelection = new ComboBox
            {
                Location = new Point(120, 100),
                Size = new Size(200, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false // Initially hidden
            };
            tabPageDebug.Controls.Add(comboBoxStateSelection);
            
            // Corporation selection for CEO role
            comboBoxCorporationSelection = new ComboBox
            {
                Location = new Point(120, 100),
                Size = new Size(200, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false // Initially hidden
            };
            tabPageDebug.Controls.Add(comboBoxCorporationSelection);
            
            // Buttons for role management
            buttonAssumeRole = new Button
            {
                Location = new Point(120, 140),
                Size = new Size(100, 30),
                Text = "Assume Role",
                Enabled = false // Initially disabled until a selection is made
            };
            buttonAssumeRole.Click += ButtonAssumeRole_Click;
            tabPageDebug.Controls.Add(buttonAssumeRole);
            
            buttonRelinquishRole = new Button
            {
                Location = new Point(230, 140),
                Size = new Size(120, 30),
                Text = "Relinquish Role"
            };
            buttonRelinquishRole.Click += ButtonRelinquishRole_Click;
            tabPageDebug.Controls.Add(buttonRelinquishRole);
            
            // Add additional role-specific information panel
            Label labelRoleInfo = new Label
            {
                Location = new Point(20, 190),
                Size = new Size(400, 200),
                Text = "Debug Information:\nUse this panel to switch between different roles and countries/states/corporations for testing purposes.",
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightYellow
            };
            tabPageDebug.Controls.Add(labelRoleInfo);
            
            // Initial population of comboboxes
            PopulateCountrySelectionComboBox();
            PopulateStateSelectionComboBox();
            PopulateCorporationSelectionComboBox();
            
            // Update current role display
            UpdateCurrentRoleDisplay();

            // Debug mode toggle button
            Button buttonToggleDebugMode = new Button
            {
                Location = new Point(20, 100),
                Size = new Size(200, 30),
                Text = "Toggle Debug Mode"
            };
            buttonToggleDebugMode.Click += (sender, e) =>
            {
                isDetailedDebugMode = !isDetailedDebugMode;
                MessageBox.Show(isDetailedDebugMode ? "Detailed Debug Mode Enabled" : "General Debug Mode Enabled", "Debug Mode Toggled");
            };
            tabPageDebug.Controls.Add(buttonToggleDebugMode);

            // Finance ListView setup
            listViewFinance.Columns.Add("Country", 120);
            listViewFinance.Columns.Add("Money Supply", 100);
            listViewFinance.Columns.Add("Reserves", 100);
            listViewFinance.Columns.Add("Base Rate", 80);
            listViewFinance.Columns.Add("Debt/GDP", 80);
            listViewFinance.Columns.Add("Inflation", 80);
            listViewFinance.Columns.Add("Credit Rating", 80);
        }
    
        // Populate the country selection combobox
        private void PopulateCountrySelectionComboBox()
        {
            comboBoxCountrySelection.Items.Clear();
            foreach (Country country in allCountries)
            {
                comboBoxCountrySelection.Items.Add(country.Name);
            }
            if (comboBoxCountrySelection.Items.Count > 0)
            {
                comboBoxCountrySelection.SelectedIndex = 0;
            }
        }
    
        // Populate the state selection combobox
        private void PopulateStateSelectionComboBox()
        {
            comboBoxStateSelection.Items.Clear();
            foreach (State state in states)
            {
                comboBoxStateSelection.Items.Add(state.Name);
            }
            if (comboBoxStateSelection.Items.Count > 0)
            {
                comboBoxStateSelection.SelectedIndex = 0;
            }
        }
    
        // Populate the corporation selection combobox
        private void PopulateCorporationSelectionComboBox()
        {
            comboBoxCorporationSelection.Items.Clear();
            if (Market.AllCorporations != null)
            {
                foreach (Corporation corp in Market.AllCorporations)
                {
                    comboBoxCorporationSelection.Items.Add(corp.Name);
                }
                if (comboBoxCorporationSelection.Items.Count > 0)
                {
                    comboBoxCorporationSelection.SelectedIndex = 0;
                }
            }
        }
    
        // Handle role type selection change
        private void ComboBoxRoleType_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Hide all entity selection comboboxes
            comboBoxCountrySelection.Visible = false;
            comboBoxStateSelection.Visible = false;
            comboBoxCorporationSelection.Visible = false;
        
            // Show the appropriate combobox based on selected role
            if (comboBoxRoleType.SelectedItem != null)
            {
                switch (comboBoxRoleType.SelectedItem.ToString())
                {
                    case "Prime Minister":
                        comboBoxCountrySelection.Visible = true;
                        break;
                    case "Governor":
                        comboBoxStateSelection.Visible = true;
                        break;
                    case "CEO":
                        comboBoxCorporationSelection.Visible = true;
                        break;
                }
            
                // Enable the assume role button if a role is selected
                buttonAssumeRole.Enabled = true;
            }
        }
    
        // Handle assume role button click
        private void ButtonAssumeRole_Click(object sender, EventArgs e)
        {
            if (comboBoxRoleType.SelectedItem == null)
            {
                MessageBox.Show("Please select a role first.");
                return;
            }
        
            string selectedRole = comboBoxRoleType.SelectedItem.ToString();
        
            switch (selectedRole)
            {
                case "Prime Minister":
                    if (comboBoxCountrySelection.SelectedItem == null)
                    {
                        MessageBox.Show("Please select a country.");
                        return;
                    }
                
                    string countryName = comboBoxCountrySelection.SelectedItem.ToString();
                    Country selectedCountry = allCountries.Find(c => c.Name == countryName);
                
                    if (selectedCountry != null)
                    {
                        playerRoleManager.AssumeRolePrimeMinister(selectedCountry);
                        playerCountry = selectedCountry; // Set the player country
                        // Update UI to reflect the new country
                        if (comboBoxCountry.Items.Contains(selectedCountry.Name))
                        {
                            comboBoxCountry.SelectedItem = selectedCountry.Name;
                        }
                    }
                    break;
                
                case "Governor":
                    if (comboBoxStateSelection.SelectedItem == null)
                    {
                        MessageBox.Show("Please select a state.");
                        return;
                    }
                
                    string stateName = comboBoxStateSelection.SelectedItem.ToString();
                    State selectedState = states.Find(s => s.Name == stateName);
                
                    if (selectedState != null)
                    {
                        playerRoleManager.AssumeRoleGovernor(selectedState);
                        // Update UI to show the selected state
                        Country stateCountry = allCountries.Find(c => c.States.Contains(selectedState));
                        if (stateCountry != null && comboBoxCountry.Items.Contains(stateCountry.Name))
                        {
                            comboBoxCountry.SelectedItem = stateCountry.Name;
                            if (comboBoxStates.Items.Contains(selectedState.Name))
                            {
                                comboBoxStates.SelectedItem = selectedState.Name;
                            }
                        }
                    }
                    break;
                
                case "CEO":
                    if (comboBoxCorporationSelection.SelectedItem == null)
                    {
                        MessageBox.Show("Please select a corporation.");
                        return;
                    }
                
                    string corporationName = comboBoxCorporationSelection.SelectedItem.ToString();
                    Corporation selectedCorporation = Market.AllCorporations.Find(c => c.Name == corporationName);
                
                    if (selectedCorporation != null)
                    {
                        playerRoleManager.AssumeRoleCEO(selectedCorporation);
                    }
                    break;
            }
        
            // Update the current role display
            UpdateCurrentRoleDisplay();
        }
    
        // Handle relinquish role button click
        private void ButtonRelinquishRole_Click(object sender, EventArgs e)
        {
            playerRoleManager.RelinquishCurrentRole();
            UpdateCurrentRoleDisplay();
        }
    
        // Update the current role display
        private void UpdateCurrentRoleDisplay()
        {
            string roleInfo = "Current Role: ";
        
            switch (playerRoleManager.CurrentRole)
            {
                case PlayerRoleType.None:
                    roleInfo += "None";
                    break;
                
                case PlayerRoleType.PrimeMinister:
                    roleInfo += $"Prime Minister of {playerRoleManager.ControlledCountry?.Name ?? "N/A"}";
                    break;
                
                case PlayerRoleType.Governor:
                    roleInfo += $"Governor of {playerRoleManager.ControlledState?.Name ?? "N/A"}";
                    break;
                
                case PlayerRoleType.CEO:
                    roleInfo += $"CEO of {playerRoleManager.ControlledCorporation?.Name ?? "N/A"}";
                    break;
            }
        
            labelCurrentRole.Text = roleInfo;
        }

        private void ButtonOpenTradeManagement_Click(object sender, EventArgs e)
        {
            // Create and show the TradeManagementForm
            var tradeManagementForm = new economy_sim.TradeManagementForm(
                playerCountry,
                allCountries,
                allCitiesInWorld,
                tradeRouteManager, 
                enhancedTradeManager, 
                globalMarket);
            
            tradeManagementForm.Show();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            var selectedCity = GetSelectedCity(); // Retrieve the selected city
            if (selectedCity != null)
            {
                DebugLogger.LogDetailedCityData(selectedCity); // Log detailed data for the selected city
            }
            DebugLogger.FinalizeLog(allCountries); // Pass the list of countries to the logger
            base.OnFormClosing(e);
        }

        // Handler for designer "Toggle Debug" button click
        private void ButtonToggleDebug_Click(object sender, EventArgs e)
        {
            bool isEnabled = DebugLogger.ToggleLogging();
            this.buttonToggleDebug.Text = $"Toggle Debug: {(isEnabled ? "ON" : "OFF")}";
            DebugLogger.Log($"Debug logging has been {(isEnabled ? "enabled" : "disabled")}");
        }

        // Update the Finance tab UI
        private void UpdateFinanceTab()
        {
            // Update main financial stats
            listViewFinance.Items.Clear();
            foreach (var country in allCountries)
            {
                var fs = country.FinancialSystem;
                var item = new ListViewItem(country.Name);
                item.SubItems.Add(fs.MoneySupply.ToString("C"));
                item.SubItems.Add(fs.NationalReserves.ToString("C"));
                item.SubItems.Add(fs.BaseInterestRate.ToString("P"));
                item.SubItems.Add(fs.DebtToGdpRatio.ToString("P"));
                item.SubItems.Add(fs.InflationRate.ToString("P"));
                item.SubItems.Add(fs.CreditRating.ToString("P"));
                listViewFinance.Items.Add(item);
                
                // Highlight player's country
                if (country == playerCountry)
                {
                    item.BackColor = Color.LightBlue;
                }
            }

            // Update bonds ListView if it exists
            var listViewBonds = tabPageFinance.Controls.OfType<ListView>()
                                          .FirstOrDefault(lv => lv != listViewFinance);
            if (listViewBonds != null && playerCountry?.FinancialSystem != null)
            {
                listViewBonds.Items.Clear();
                foreach (var bond in playerCountry.FinancialSystem.OutstandingBonds)
                {
                    var bondItem = new ListViewItem(bond.Id.ToString().Substring(0, 8)); // Short GUID
                    bondItem.SubItems.Add(bond.Type.ToString());
                    bondItem.SubItems.Add(bond.PrincipalAmount.ToString("C"));
                    bondItem.SubItems.Add(bond.InterestRate.ToString("P"));
                    bondItem.SubItems.Add(bond.MaturityDate.ToShortDateString());
                    bondItem.SubItems.Add(bond.OwnerId);
                    bondItem.SubItems.Add(bond.IsDefaulted ? "Defaulted" : "Active");
                    
                    if (bond.IsDefaulted)
                    {
                        bondItem.BackColor = Color.LightPink;
                    }
                    else if (bond.MaturityDate <= DateTime.Now)
                    {
                        bondItem.BackColor = Color.LightYellow;
                    }
                    
                    listViewBonds.Items.Add(bondItem);
                }
            }
        }

        private void InitializeFinanceTab()
        {
            // Main financial stats ListView
            listViewFinance.Columns.Add("Country", 120);
            listViewFinance.Columns.Add("Money Supply", 100);
            listViewFinance.Columns.Add("Reserves", 100);
            listViewFinance.Columns.Add("Base Rate", 80);
            listViewFinance.Columns.Add("Debt/GDP", 80);
            listViewFinance.Columns.Add("Inflation", 80);
            listViewFinance.Columns.Add("Credit Rating", 80);

            // Add bond management ListView
            ListView listViewBonds = new ListView();
            listViewBonds.View = View.Details;
            listViewBonds.FullRowSelect = true;
            listViewBonds.GridLines = true;
            listViewBonds.Location = new Point(10, 180);
            listViewBonds.Size = new Size(800, 200);
            listViewBonds.Columns.Add("Bond ID", 100);
            listViewBonds.Columns.Add("Type", 80);
            listViewBonds.Columns.Add("Principal", 100);
            listViewBonds.Columns.Add("Interest Rate", 80);
            listViewBonds.Columns.Add("Maturity Date", 120);
            listViewBonds.Columns.Add("Owner", 100);
            listViewBonds.Columns.Add("Status", 80);
            tabPageFinance.Controls.Add(listViewBonds);

            // Add bond issuance controls
            Button buttonIssueBond = new Button
            {
                Text = "Issue New Bond",
                Location = new Point(10, 390),
                Size = new Size(120, 30)
            };
            buttonIssueBond.Click += (s, e) => OpenBondIssuanceDialog();
            tabPageFinance.Controls.Add(buttonIssueBond);

            Button buttonProcessBonds = new Button
            {
                Text = "Process Maturities",
                Location = new Point(140, 390),
                Size = new Size(120, 30)
            };
            buttonProcessBonds.Click += (s, e) => ProcessBondMaturities();
            tabPageFinance.Controls.Add(buttonProcessBonds);
        }

        private void OpenBondIssuanceDialog()
        {
            if (playerCountry?.FinancialSystem == null) return;

            // Create bond issuance form with necessary controls
            Form bondForm = new Form
            {
                Text = "Issue New Bond",
                Size = new Size(400, 300),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent
            };

            ComboBox bondTypeCombo = new ComboBox
            {
                Location = new Point(120, 20),
                Size = new Size(200, 20)
            };
            bondTypeCombo.Items.AddRange(Enum.GetNames(typeof(BondType)));
            bondTypeCombo.SelectedIndex = 0;

            NumericUpDown principalInput = new NumericUpDown
            {
                Location = new Point(120, 60),
                Size = new Size(200, 20),
                Maximum = 1000000000,
                Minimum = 1000,
                Increment = 1000,
                Value = 100000
            };

            NumericUpDown interestInput = new NumericUpDown
            {
                Location = new Point(120, 100),
                Size = new Size(200, 20),
                DecimalPlaces = 2,
                Maximum = 20,
                Minimum = 0.1m,
                Increment = 0.25m,
                Value = 5
            };

            NumericUpDown maturityInput = new NumericUpDown
            {
                Location = new Point(120, 140),
                Size = new Size(200, 20),
                Maximum = 30,
                Minimum = 1,
                Value = 5
            };

            bondForm.Controls.AddRange(new Control[] {
                new Label { Text = "Bond Type:", Location = new Point(20, 23), Size = new Size(100, 20) },
                new Label { Text = "Principal:", Location = new Point(20, 63), Size = new Size(100, 20) },
                new Label { Text = "Interest Rate (%):", Location = new Point(20, 103), Size = new Size(100, 20) },
                new Label { Text = "Maturity (Years):", Location = new Point(20, 143), Size = new Size(100, 20) },
                bondTypeCombo,
                principalInput,
                interestInput,
                maturityInput
            });

            Button issueButton = new Button
            {
                Text = "Issue Bond",
                DialogResult = DialogResult.OK,
                Location = new Point(120, 200),
                Size = new Size(100, 30)
            };
            bondForm.Controls.Add(issueButton);

            Button cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(230, 200),
                Size = new Size(100, 30)
            };
            bondForm.Controls.Add(cancelButton);

            if (bondForm.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var bondType = (BondType)Enum.Parse(typeof(BondType), bondTypeCombo.SelectedItem.ToString());
                    playerCountry.FinancialSystem.IssueBond(
                        playerCountry.Name, // Owner is the country itself initially
                        principalInput.Value,
                        (float)(interestInput.Value / 100.0m), // Convert percentage to decimal
                        (int)maturityInput.Value,
                        bondType
                    );
                    UpdateFinanceTab(); // Refresh display
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Bond Issuance Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ProcessBondMaturities()
        {
            if (playerCountry?.FinancialSystem == null) return;

            var fs = playerCountry.FinancialSystem;
            var bonds = fs.OutstandingBonds.ToList(); // Get snapshot of current bonds
            
            foreach (var bond in bonds)
            {
                if (bond.MaturityDate <= DateTime.Now && !bond.IsDefaulted)
                {
                    fs.ProcessBondMaturity(bond.Id);
                }
            }

            UpdateFinanceTab(); // Refresh display after processing
        }
    }
}
