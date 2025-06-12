// This file has been consolidated into MainGame.cs
        
        // Initialize Debug tab UI
        private void InitializeDebugTab()
        {
            // Debug logging toggle
            buttonToggleDebug = new Button
            {
                Location = new Point(20, 20),
                Size = new Size(150, 30),
                Text = "Toggle Debug Logging: ON"
            };
            buttonToggleDebug.Click += ButtonToggleDebug_Click;
            tabPageDebug.Controls.Add(buttonToggleDebug);

            // Current role status (moved down to accommodate the new button)
            labelCurrentRole = new Label
            {
                Location = new Point(20, 60),
                AutoSize = true,
                Text = "Current Role: None"
            };
            tabPageDebug.Controls.Add(labelCurrentRole);
            
            // Role type selection (adjusted Y positions)
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
                Location = new Point(20, 140),
                AutoSize = true,
                Text = "Select Entity:"
            };
            tabPageDebug.Controls.Add(labelEntitySelection);
            
            // Country selection for Prime Minister role
            comboBoxCountrySelection = new ComboBox
            {
                Location = new Point(120, 140),
                Size = new Size(200, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false // Initially hidden
            };
            tabPageDebug.Controls.Add(comboBoxCountrySelection);
            
            // State selection for Governor role
            comboBoxStateSelection = new ComboBox
            {
                Location = new Point(120, 140),
                Size = new Size(200, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false // Initially hidden
            };
            tabPageDebug.Controls.Add(comboBoxStateSelection);
            
            // Corporation selection for CEO role
            comboBoxCorporationSelection = new ComboBox
            {
                Location = new Point(120, 140),
                Size = new Size(200, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Visible = false // Initially hidden
            };
            tabPageDebug.Controls.Add(comboBoxCorporationSelection);
            
            // Buttons for role management
            buttonAssumeRole = new Button
            {
                Location = new Point(120, 180),
                Size = new Size(100, 30),
                Text = "Assume Role",
                Enabled = false // Initially disabled until a selection is made
            };
            buttonAssumeRole.Click += ButtonAssumeRole_Click;
            tabPageDebug.Controls.Add(buttonAssumeRole);
            
            buttonRelinquishRole = new Button
            {
                Location = new Point(230, 180),
                Size = new Size(120, 30),
                Text = "Relinquish Role"
            };
            buttonRelinquishRole.Click += ButtonRelinquishRole_Click;
            tabPageDebug.Controls.Add(buttonRelinquishRole);
            
            // Add additional role-specific information panel
            Label labelRoleInfo = new Label
            {
                Location = new Point(20, 230),
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

        // Handle debug logging toggle button click
        private void ButtonToggleDebug_Click(object sender, EventArgs e)
        {
            bool isEnabled = DebugLogger.ToggleLogging();
            buttonToggleDebug.Text = $"Toggle Debug Logging: {(isEnabled ? "ON" : "OFF")}";
            DebugLogger.Log($"Debug logging has been {(isEnabled ? "enabled" : "disabled")}");
        }
    }
}
