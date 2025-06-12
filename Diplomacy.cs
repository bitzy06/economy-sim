using System;
using System.Collections.Generic;

namespace StrategyGame
{
    public enum TradeStatus
    {
        Proposed,
        Active,
        Cancelled,
        Completed,
        Rejected
    }

    public class TradeAgreement
    {
        public Guid Id { get; private set; }
        public string FromCountryName { get; set; } // Name of the exporting country
        public string ToCountryName { get; set; }   // Name of the importing country
        public string ResourceName { get; set; }    // e.g., "Iron", "Grain", "Oil"
        public double Quantity { get; set; }        // Amount of resource to trade
        public double PricePerUnit { get; set; }    // Price for each unit of the resource
        public TradeStatus Status { get; set; }
        public int DurationTurns { get; set; } // How many game turns this agreement lasts, 0 for indefinite
        public int TurnsRemaining { get; set; } // Countdown for active agreements

        public TradeAgreement(string fromCountry, string toCountry, string resource, double quantity, double price, int duration = 1)
        {
            Id = Guid.NewGuid();
            FromCountryName = fromCountry;
            ToCountryName = toCountry;
            ResourceName = resource;
            Quantity = quantity;
            PricePerUnit = price;
            Status = TradeStatus.Proposed;
            DurationTurns = duration;
            TurnsRemaining = duration; // Initialize with full duration
        }

        public double TotalValue => Quantity * PricePerUnit;
    }

    // Placeholder for other diplomacy-related classes or enums in the future
    // public class Alliance
    // {
    //     // ...
    // }

    public enum DiplomaticStance
    {
        War,
        Peace,
        Alliance,
        NonAggressionPact,
        DefensivePact,
        TradeEmbargo,
        Vassal,
        Overlord,
        MilitaryAccess
    }

    public class DiplomaticRelation
    {
        public string Country1Name { get; set; }
        public string Country2Name { get; set; }
        public DiplomaticStance Stance { get; set; }
        public int TurnsRemaining { get; set; } // For timed agreements like NonAggressionPact
        public DateTime StartDate { get; set; }

        public DiplomaticRelation(string c1, string c2, DiplomaticStance stance, int duration = 0)
        {
            Country1Name = c1;
            Country2Name = c2;
            Stance = stance;
            TurnsRemaining = duration;
            StartDate = DateTime.UtcNow; // Or game-specific date/turn
        }
    }

    // Example of a more specific pact
    public class AlliancePact : DiplomaticRelation
    {
        public bool IsDefensive { get; set; }
        public bool IsOffensive { get; set; }

        public AlliancePact(string c1, string c2, bool defensive, bool offensive, int duration = 0)
            : base(c1, c2, DiplomaticStance.Alliance, duration)
        {
            IsDefensive = defensive;
            IsOffensive = offensive;
        }
    }

    // Placeholder for managing all diplomatic relations for a country or globally
    public class DiplomacyManager
    {
        public List<TradeAgreement> AllTradeAgreements { get; private set; }
        public List<DiplomaticRelation> AllDiplomaticRelations { get; private set; }
        private List<Country> allGameCountries; // Added to access country data

        public DiplomacyManager(List<Country> gameCountries) // Updated constructor
        {
            AllTradeAgreements = new List<TradeAgreement>();
            AllDiplomaticRelations = new List<DiplomaticRelation>();
            allGameCountries = gameCountries; // Store the reference
        }

        // --- Trade Agreement Management ---
        public void ProposeTradeAgreement(TradeAgreement agreement)
        {
            // Logic to add a new proposed trade agreement
            // Potentially notify the target country
            agreement.Status = TradeStatus.Proposed;
            AllTradeAgreements.Add(agreement);
            Console.WriteLine($"Trade proposed from {agreement.FromCountryName} to {agreement.ToCountryName} for {agreement.Quantity} of {agreement.ResourceName}.");
        }

        public void AcceptTradeAgreement(Guid agreementId, string acceptingCountryName)
        {
            var agreement = AllTradeAgreements.Find(ta => ta.Id == agreementId && ta.ToCountryName == acceptingCountryName && ta.Status == TradeStatus.Proposed);
            if (agreement != null)
            {
                agreement.Status = TradeStatus.Active;
                Console.WriteLine($"Trade agreement {agreementId} accepted by {acceptingCountryName}.");
                // Further logic: deduct resources, transfer funds on first turn if applicable
            }
            else
            {
                Console.WriteLine($"Failed to accept trade agreement {agreementId} for {acceptingCountryName}. Not found or not in proposed state.");
            }
        }

        public void RejectTradeAgreement(Guid agreementId, string rejectingCountryName)
        {
            var agreement = AllTradeAgreements.Find(ta => ta.Id == agreementId && ta.ToCountryName == rejectingCountryName && ta.Status == TradeStatus.Proposed);
            if (agreement != null)
            {
                agreement.Status = TradeStatus.Rejected;
                Console.WriteLine($"Trade agreement {agreementId} rejected by {rejectingCountryName}.");
            }
             else
            {
                Console.WriteLine($"Failed to reject trade agreement {agreementId} for {rejectingCountryName}. Not found or not in proposed state.");
            }
        }

        public void CancelTradeAgreement(Guid agreementId, string cancellingCountryName)
        {
            var agreement = AllTradeAgreements.Find(ta => ta.Id == agreementId && (ta.FromCountryName == cancellingCountryName || ta.ToCountryName == cancellingCountryName) && ta.Status == TradeStatus.Active);
            if (agreement != null)
            {
                agreement.Status = TradeStatus.Cancelled;
                Console.WriteLine($"Trade agreement {agreementId} cancelled by {cancellingCountryName}.");
            }
            else
            {
                Console.WriteLine($"Failed to cancel trade agreement {agreementId} for {cancellingCountryName}. Not found or not active.");
            }
        }

        public List<TradeAgreement> GetTradeAgreementsForCountry(string countryName)
        {
            return AllTradeAgreements.FindAll(ta => ta.FromCountryName == countryName || ta.ToCountryName == countryName);
        }

        public List<TradeAgreement> GetActiveTradeAgreements()
        {
            return AllTradeAgreements.FindAll(ta => ta.Status == TradeStatus.Active);
        }

        // --- Diplomatic Relation Management ---
        public void EstablishDiplomaticRelation(string country1Name, string country2Name, DiplomaticStance stance, int duration = 0)
        {
            // Avoid duplicate relations, update if exists
            var existingRelation = AllDiplomaticRelations.Find(dr => 
                (dr.Country1Name == country1Name && dr.Country2Name == country2Name) || 
                (dr.Country1Name == country2Name && dr.Country2Name == country1Name));

            if (existingRelation != null)
            {
                existingRelation.Stance = stance;
                existingRelation.TurnsRemaining = duration;
                existingRelation.StartDate = DateTime.UtcNow; // Reset start date on change
                 Console.WriteLine($"Diplomatic stance between {country1Name} and {country2Name} updated to {stance}.");
            }
            else
            {
                var newRelation = new DiplomaticRelation(country1Name, country2Name, stance, duration);
                AllDiplomaticRelations.Add(newRelation);
                Console.WriteLine($"Diplomatic stance {stance} established between {country1Name} and {country2Name}.");
            }
        }

        public DiplomaticRelation GetRelation(string country1Name, string country2Name)
        {
            return AllDiplomaticRelations.Find(dr => 
                (dr.Country1Name == country1Name && dr.Country2Name == country2Name) || 
                (dr.Country1Name == country2Name && dr.Country2Name == country1Name));
        }

        // --- Turn Processing ---
        public void ProcessTurnEnd()
        {
            // Update trade agreements (countdown duration, process recurring trades)
            List<TradeAgreement> completedAgreements = new List<TradeAgreement>();
            // Iterate on a copy if modifying the list during iteration (e.g., removing completed agreements)
            foreach (var agreement in new List<TradeAgreement>(AllTradeAgreements)) // Iterate on a copy
            {
                if (agreement.Status == TradeStatus.Active)
                {
                    // Actual resource and budget transfer
                    Country fromCountry = allGameCountries.Find(c => c.Name == agreement.FromCountryName);
                    Country toCountry = allGameCountries.Find(c => c.Name == agreement.ToCountryName);

                    if (fromCountry != null && toCountry != null)
                    {
                        double totalValue = agreement.Quantity * agreement.PricePerUnit;
                        // Check if exporter has enough resources and importer has enough budget
                        if (fromCountry.GetResourceAmount(agreement.ResourceName) >= agreement.Quantity && toCountry.Budget >= totalValue)
                        {
                            fromCountry.RemoveResource(agreement.ResourceName, agreement.Quantity);
                            toCountry.AddResource(agreement.ResourceName, agreement.Quantity);

                            fromCountry.Budget += totalValue;
                            toCountry.Budget -= totalValue;

                            Console.WriteLine($"Trade executed: {agreement.Quantity} of {agreement.ResourceName} from {fromCountry.Name} to {toCountry.Name} for ${totalValue}.");

                            if (agreement.DurationTurns > 0) // Timed agreement
                            {
                                agreement.TurnsRemaining--;
                                if (agreement.TurnsRemaining <= 0)
                                {
                                    agreement.Status = TradeStatus.Completed;
                                    completedAgreements.Add(agreement);
                                    Console.WriteLine($"Trade agreement {agreement.Id} between {agreement.FromCountryName} and {agreement.ToCountryName} has completed.");
                                }
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Trade failed for agreement {agreement.Id} between {fromCountry.Name} and {toCountry.Name}. Insufficient resources or budget.");
                            // Optionally, cancel the agreement or mark it as failed for this turn
                            // agreement.Status = TradeStatus.Cancelled; // Or a new status like FailedThisTurn
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Trade failed: Could not find one or both countries for agreement {agreement.Id}.");
                        // agreement.Status = TradeStatus.Cancelled; // Or handle as error
                    }
                }
            }
            // Remove completed agreements from the main list if necessary, or just leave them with Completed status
            // AllTradeAgreements.RemoveAll(ta => ta.Status == TradeStatus.Completed);

            // Update diplomatic relations (countdown durations for pacts)
            List<DiplomaticRelation> expiredRelations = new List<DiplomaticRelation>();
            foreach (var relation in AllDiplomaticRelations)
            {
                if (relation.TurnsRemaining > 0)
                {
                    relation.TurnsRemaining--;
                    if (relation.TurnsRemaining <= 0)
                    {
                        // Logic for what happens when a timed pact expires, e.g., revert to Peace
                        Console.WriteLine($"Timed diplomatic stance {relation.Stance} between {relation.Country1Name} and {relation.Country2Name} has expired.");
                        // Potentially remove or change stance, e.g., relation.Stance = DiplomaticStance.Peace;
                        // For now, just marking as expired for potential removal or change
                        expiredRelations.Add(relation); 
                    }
                }
            }
            // Optionally remove or update expired relations from AllDiplomaticRelations
        }
    }
}
