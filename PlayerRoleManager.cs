using System;
using System.Collections.Generic;
using System.Linq;

namespace StrategyGame
{
    public enum PlayerRoleType
    {
        None,
        PrimeMinister,
        Governor,
        CEO
    }

    public class PlayerRoleManager
    {
        public PlayerRoleType CurrentRole { get; private set; }

        // Properties specific to roles
        public Country ControlledCountry { get; private set; } // For Prime Minister
        public State ControlledState { get; private set; }     // For Governor
        public Corporation ControlledCorporation { get; private set; } // For CEO

        public PlayerRoleManager()
        {
            CurrentRole = PlayerRoleType.None;
        }

        public void AssumeRolePrimeMinister(Country country)
        {
            RelinquishCurrentRole(); // Ensure previous role settings are cleared
            CurrentRole = PlayerRoleType.PrimeMinister;
            ControlledCountry = country;
            Console.WriteLine($"Player assumed role: Prime Minister of {country?.Name ?? "N/A"}");
        }

        public void AssumeRoleGovernor(State state)
        {
            RelinquishCurrentRole();
            CurrentRole = PlayerRoleType.Governor;
            ControlledState = state;
            Console.WriteLine($"Player assumed role: Governor of {state?.Name ?? "N/A"}");
        }

        public void AssumeRoleCEO(Corporation corporation)
        {
            RelinquishCurrentRole();
            CurrentRole = PlayerRoleType.CEO;
            ControlledCorporation = corporation;
            if (ControlledCorporation != null)
            {
                ControlledCorporation.IsPlayerControlled = true;
                // Ensure the player-controlled corporation is in the global list
                if (!Market.AllCorporations.Contains(ControlledCorporation))
                {
                    Market.AllCorporations.Add(ControlledCorporation);
                    Console.WriteLine($"Warning: Player-controlled corporation '{ControlledCorporation.Name}' was not in global list and has been added.");
                }
            }
            Console.WriteLine($"Player assumed role: CEO of {corporation?.Name ?? "N/A"}");
        }

        public void RelinquishCurrentRole()
        {
            if (CurrentRole == PlayerRoleType.CEO && ControlledCorporation != null)
            {
                ControlledCorporation.IsPlayerControlled = false; // AI would take over this corp
            }
            CurrentRole = PlayerRoleType.None;
            ControlledCountry = null;
            ControlledState = null;
            ControlledCorporation = null;
            Console.WriteLine("Player role relinquished.");
        }

        // --- Placeholder Action Methods ---

        // Prime Minister Actions
        public bool SetNationalPolicy(string policyName, string policyValue)
        {
            if (CurrentRole != PlayerRoleType.PrimeMinister || ControlledCountry == null) return false;
            Console.WriteLine($"PM of {ControlledCountry.Name}: Setting {policyName} to {policyValue}");
            // Example: ControlledCountry.Policies[policyName] = policyValue;
            return true;
        }

        // Governor Actions
        public bool SetStatePolicy(string policyName, string policyValue)
        {
            if (CurrentRole != PlayerRoleType.Governor || ControlledState == null) return false;
            Console.WriteLine($"Governor of {ControlledState.Name}: Setting {policyName} to {policyValue}");
            // Example: ControlledState.Policies[policyName] = policyValue;
            return true;
        }

        // CEO Actions
        public bool BuildFactoryAsCEO(string factoryType, City locationCity)
        {
            if (CurrentRole != PlayerRoleType.CEO || ControlledCorporation == null || locationCity == null) return false;
            
            // Simplified check: Does corp have enough budget? (e.g., > 50000)
            double buildCost = 50000; // Example cost
            if (ControlledCorporation.Budget >= buildCost)
            {
                ControlledCorporation.Budget -= buildCost;
                // Create and add factory - this logic would be more complex
                // Factory newFactory = new Factory(factoryType + " Plant", 5); // Example capacity
                // newFactory.OwnerCorporation = ControlledCorporation;
                // locationCity.Factories.Add(newFactory);
                // ControlledCorporation.AddFactory(newFactory);
                Console.WriteLine($"CEO of {ControlledCorporation.Name}: Successfully ordered building of {factoryType} in {locationCity.Name}. Budget: {ControlledCorporation.Budget}");
                return true;
            }
            else
            {
                Console.WriteLine($"CEO of {ControlledCorporation.Name}: Insufficient funds to build {factoryType}. Needs {buildCost}, Has {ControlledCorporation.Budget}");
                return false;
            }
        }
    }
} 