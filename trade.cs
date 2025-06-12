using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing; // For Point type

namespace StrategyGame
{
    #region TradeRoutes

    public enum RouteType
    {
        Land,
        Sea,
        River,
        Air
    }

    public class TradeRoute
    {
        public Guid Id { get; private set; }
        public string Name { get; set; }
        public RouteType Type { get; set; }
        public City StartCity { get; set; }
        public City EndCity { get; set; }
        public double Distance { get; set; } // In km
        public double Capacity { get; set; } // Max volume per turn
        public double CurrentUsage { get; set; } // Current volume being used
        public double TransportCost { get; set; } // Per unit per km
        public double MaintenanceCost { get; set; } // Per turn
        public Dictionary<string, int> GoodsInTransit { get; set; } // Goods currently in transit, with turns until delivery
        public List<string> RestrictedGoods { get; set; } // Goods that can't be transported on this route
        public bool IsPlayerOwned { get; set; }
        public double DangerLevel { get; set; } // Risk of loss during transport (0.0 to 1.0)
        public List<TradeRouteUpgrade> Upgrades { get; set; }
        public bool IsBlocked { get; set; } // For blockades, damage, etc.
        
        public TradeRoute(City start, City end, RouteType type)
        {
            Id = Guid.NewGuid();
            StartCity = start;
            EndCity = end;
            Type = type;
            
            // Generate a name based on cities and type
            Name = $"{start.Name} to {end.Name} {type} Route";
            
            // Calculate distance (simplified here - could use coordinates in a real implementation)
            Distance = CalculateDistance(start, end);
            
            // Initialize based on route type
            InitializeRouteParameters();
            
            GoodsInTransit = new Dictionary<string, int>();
            RestrictedGoods = new List<string>();
            Upgrades = new List<TradeRouteUpgrade>();
            IsBlocked = false;
            DangerLevel = 0.05; // 5% base danger
            IsPlayerOwned = false;
        }
        
        private double CalculateDistance(City start, City end)
        {
            // In a real implementation, cities would have coordinates
            // Here we'll just generate a reasonable distance
            return Math.Max(100, new Random().Next(50, 1000));
        }
        
        private void InitializeRouteParameters()
        {
            // Set default values based on route type
            switch (Type)
            {
                case RouteType.Land:
                    Capacity = 100;
                    TransportCost = 0.5;
                    MaintenanceCost = 50;
                    break;
                case RouteType.Sea:
                    Capacity = 500;
                    TransportCost = 0.1;
                    MaintenanceCost = 200;
                    break;
                case RouteType.River:
                    Capacity = 200;
                    TransportCost = 0.3;
                    MaintenanceCost = 100;
                    break;
                case RouteType.Air:
                    Capacity = 50;
                    TransportCost = 1.0;
                    MaintenanceCost = 500;
                    break;
            }
        }
        
        public double CalculateTransportationCost(string goodName, int quantity)
        {
            // Base cost calculation
            double baseCost = quantity * TransportCost * Distance;
            
            // Apply modifiers from upgrades
            foreach (var upgrade in Upgrades)
            {
                baseCost *= upgrade.CostModifier;
            }
            
            // Factor in the good's properties if available
            if (Market.GoodDefinitions.ContainsKey(goodName))
            {
                Good goodDef = Market.GoodDefinitions[goodName];
                
                // Luxury or bulky goods may cost more to transport
                if (goodDef.Category == GoodCategory.ConsumerProduct)
                {
                    baseCost *= 1.2; // 20% premium for consumer goods
                }
                else if (goodDef.Category == GoodCategory.RawMaterial)
                {
                    baseCost *= 1.5; // 50% premium for bulky raw materials
                }
            }
            
            return baseCost;
        }
        
        public bool CanTransportGood(string goodName, int quantity)
        {
            // Check if good is restricted
            if (RestrictedGoods.Contains(goodName))
                return false;
                
            // Check capacity
            if (CurrentUsage + quantity > Capacity)
                return false;
                
            // Check if route is blocked
            if (IsBlocked)
                return false;
                
            return true;
        }
        
        public void ProcessGoodsInTransit()
        {
            // Process goods currently in transit
            var deliveredGoods = new List<string>();
            
            foreach (var entry in GoodsInTransit)
            {
                string goodName = entry.Key;
                int turnsRemaining = entry.Value;
                
                if (turnsRemaining <= 1)
                {
                    // Ready for delivery, mark for removal from transit
                    deliveredGoods.Add(goodName);
                    
                    // In a real implementation, we would add the good to the destination city's stockpile
                    // and update the trade statistics
                }
                else
                {
                    // Reduce remaining turns
                    GoodsInTransit[goodName] = turnsRemaining - 1;
                }
            }
            
            // Remove delivered goods from transit
            foreach (var good in deliveredGoods)
            {
                GoodsInTransit.Remove(good);
            }
        }
        
        public void ApplyUpgrade(TradeRouteUpgrade upgrade)
        {
            Upgrades.Add(upgrade);
            
            // Apply upgrade effects
            Capacity *= upgrade.CapacityModifier;
            TransportCost *= upgrade.CostModifier;
            DangerLevel *= upgrade.DangerModifier;
            MaintenanceCost *= upgrade.MaintenanceModifier;
        }
        
        public void UpdateDangerLevel(double militaryPresence, double relationshipQuality)
        {
            // Higher military presence reduces danger
            double militaryFactor = Math.Max(0, 1.0 - militaryPresence * 0.5);
            
            // Better relationships reduce danger
            double relationshipFactor = Math.Max(0.5, 1.0 - relationshipQuality * 0.5);
            
            // Update danger level with these factors
            DangerLevel = 0.05 * militaryFactor * relationshipFactor;
            
            // Apply route type specific risks
            if (Type == RouteType.Sea)
            {
                DangerLevel *= 1.5; // Sea routes are more dangerous (pirates, etc.)
            }
            
            // Cap danger level
            DangerLevel = Math.Min(0.5, Math.Max(0.01, DangerLevel));
        }
    }
    
    public class TradeRouteUpgrade
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public double Cost { get; set; }
        public double CapacityModifier { get; set; } = 1.0;
        public double CostModifier { get; set; } = 1.0;
        public double DangerModifier { get; set; } = 1.0;
        public double MaintenanceModifier { get; set; } = 1.0;
        public List<RouteType> ApplicableRouteTypes { get; set; }
        
        public TradeRouteUpgrade(string name, string description, double cost)
        {
            Name = name;
            Description = description;
            Cost = cost;
            ApplicableRouteTypes = new List<RouteType>();
        }
    }
    
    public class TradeRouteManager
    {
        public List<TradeRoute> AllTradeRoutes { get; private set; }
        
        public TradeRouteManager()
        {
            AllTradeRoutes = new List<TradeRoute>();
        }
        
        public TradeRoute CreateNewRoute(City start, City end, RouteType type)
        {
            var newRoute = new TradeRoute(start, end, type);
            AllTradeRoutes.Add(newRoute);
            return newRoute;
        }
        
        public List<TradeRoute> GetRoutesForCity(City city)
        {
            return AllTradeRoutes.Where(r => r.StartCity == city || r.EndCity == city).ToList();
        }
        
        public List<TradeRoute> FindPossibleRoutes(City start, City end)
        {
            return AllTradeRoutes.Where(r => 
                (r.StartCity == start && r.EndCity == end) || 
                (r.StartCity == end && r.EndCity == start)).ToList();
        }
        
        public void UpdateAllRoutes()
        {
            foreach (var route in AllTradeRoutes)
            {
                // Process goods in transit
                route.ProcessGoodsInTransit();
                
                // Reset current usage for next turn
                route.CurrentUsage = 0;
                
                // Other periodic updates could go here
            }
        }
        
        public double CalculateTotalMaintenanceCost()
        {
            return AllTradeRoutes.Sum(r => r.MaintenanceCost);
        }
        
        public bool DestroyRoute(Guid routeId)
        {
            var route = AllTradeRoutes.FirstOrDefault(r => r.Id == routeId);
            if (route != null)
            {
                // Handle any goods in transit - they could be lost or returned
                AllTradeRoutes.Remove(route);
                return true;
            }
            return false;
        }
        
        // Common upgrades that can be applied to routes
        public static List<TradeRouteUpgrade> GetAvailableUpgrades()
        {
            var upgrades = new List<TradeRouteUpgrade>();
            
            // Land route upgrades
            var roadImprovement = new TradeRouteUpgrade(
                "Road Improvement", 
                "Better roads allow faster transportation and higher capacity.",
                5000)
            {
                CapacityModifier = 1.5,
                CostModifier = 0.8,
                DangerModifier = 0.9,
                MaintenanceModifier = 1.2
            };
            roadImprovement.ApplicableRouteTypes.Add(RouteType.Land);
            upgrades.Add(roadImprovement);
            
            // Sea route upgrades
            var harborExpansion = new TradeRouteUpgrade(
                "Harbor Expansion",
                "Larger harbors can handle more ships and cargo.",
                15000)
            {
                CapacityModifier = 2.0,
                CostModifier = 0.9,
                DangerModifier = 1.0,
                MaintenanceModifier = 1.5
            };
            harborExpansion.ApplicableRouteTypes.Add(RouteType.Sea);
            upgrades.Add(harborExpansion);
            
            // Universal upgrades
            var securityPatrols = new TradeRouteUpgrade(
                "Security Patrols",
                "Regular patrols reduce the risk of robbery and piracy.",
                8000)
            {
                CapacityModifier = 1.0,
                CostModifier = 1.1,
                DangerModifier = 0.5,
                MaintenanceModifier = 1.3
            };
            securityPatrols.ApplicableRouteTypes.AddRange(new[] { RouteType.Land, RouteType.Sea, RouteType.River });
            upgrades.Add(securityPatrols);
            
            return upgrades;
        }
    }

    #endregion
    
    #region EnhancedTrade

    public enum TariffType
    {
        None,           // No tariff
        Fixed,          // Fixed amount per unit
        AdValorem,      // Percentage of good's value
        Quota,          // Fixed limit on imports
        MostFavored,    // Lowest tariff rate given to any nation
        Preferential,   // Special reduced rate
        Prohibitive     // Effectively banned (extremely high tariff)
    }
    
    public class EnhancedTradeAgreement : TradeAgreement
    {
        // Additional properties for enhanced trade agreements
        public TariffType TariffType { get; set; }
        public double TariffRate { get; set; } // Percentage or fixed amount depending on type
        public Dictionary<string, double> GoodSpecificTariffs { get; set; } // For good-specific tariff rates
        public int ImportQuota { get; set; } // Maximum units allowed if using quota
        public List<string> QuotaExemptGoods { get; set; } // Goods exempted from quota restrictions
        public bool MutualMostFavoredNation { get; set; } // Whether both parties grant MFN status to each other
        public string TreatyName { get; set; } // For named treaties (e.g., "Franco-German Trade Pact")
        public List<string> ParticipatingCountries { get; set; } // For multilateral agreements
        public int RenegotiationPeriod { get; set; } // Turns before renegotiation is required
        public bool AutoRenew { get; set; } // Whether treaty auto-renews at end of term
        public double BreakingPenalty { get; set; } // Economic penalty for breaking the agreement early
        public int DiplomaticImpact { get; set; } // Effect on diplomatic relations if broken
        
        // Trade embargo-specific properties
        public bool IsEmbargo { get; set; }
        public List<string> EmbargoDemandsMet { get; set; } // Conditions that would lift embargo
        
        // Constructor for standard bilateral trade agreement (extends base TradeAgreement)
        public EnhancedTradeAgreement(string fromCountry, string toCountry, string resource, 
                                     double quantity, double price, int duration) 
            : base(fromCountry, toCountry, resource, quantity, price, duration)
        {
            TariffType = TariffType.None;
            TariffRate = 0.0;
            GoodSpecificTariffs = new Dictionary<string, double>();
            ImportQuota = 0; // Unlimited
            QuotaExemptGoods = new List<string>();
            MutualMostFavoredNation = false;
            TreatyName = $"{fromCountry}-{toCountry} Trade Agreement";
            ParticipatingCountries = new List<string> { fromCountry, toCountry };
            RenegotiationPeriod = duration;
            AutoRenew = false;
            BreakingPenalty = 0;
            DiplomaticImpact = 0;
            IsEmbargo = false;
            EmbargoDemandsMet = new List<string>();
        }
        
        // Constructor for multilateral trade agreement
        public EnhancedTradeAgreement(List<string> countries, string treatyName, int duration)
            : base(countries[0], countries.Count > 1 ? countries[1] : countries[0], "", 0, 0, duration)
        {
            TariffType = TariffType.None;
            TariffRate = 0.0;
            GoodSpecificTariffs = new Dictionary<string, double>();
            ImportQuota = 0; // Unlimited
            QuotaExemptGoods = new List<string>();
            MutualMostFavoredNation = false;
            TreatyName = treatyName;
            ParticipatingCountries = new List<string>(countries);
            RenegotiationPeriod = duration;
            AutoRenew = false;
            BreakingPenalty = 0;
            DiplomaticImpact = 0;
            IsEmbargo = false;
            EmbargoDemandsMet = new List<string>();
        }
        
        // Method to apply tariffs to a trade of goods
        public double CalculateTariff(string goodName, double quantity, double basePrice)
        {
            // Check for good-specific tariffs first
            if (GoodSpecificTariffs.ContainsKey(goodName))
            {
                double goodSpecificTariff = GoodSpecificTariffs[goodName];
                
                switch (TariffType)
                {
                    case TariffType.Fixed:
                        return quantity * goodSpecificTariff;
                        
                    case TariffType.AdValorem:
                        return quantity * basePrice * (goodSpecificTariff / 100.0);
                        
                    case TariffType.Prohibitive:
                        return quantity * basePrice * 5.0; // 500% value tariff
                        
                    default:
                        return 0;
                }
            }
            
            // Otherwise use the general tariff rate
            switch (TariffType)
            {
                case TariffType.None:
                    return 0;
                    
                case TariffType.Fixed:
                    return quantity * TariffRate;
                    
                case TariffType.AdValorem:
                    return quantity * basePrice * (TariffRate / 100.0);
                    
                case TariffType.Quota:
                    // No tariff until quota is exceeded, then prohibitive
                    if (ImportQuota <= 0 || quantity <= ImportQuota)
                        return 0;
                    else
                        return (quantity - ImportQuota) * basePrice * 5.0; // 500% value tariff on excess
                    
                case TariffType.Prohibitive:
                    return quantity * basePrice * 5.0; // 500% value tariff
                    
                default:
                    return 0;
            }
        }
        
        // Check if a particular trade is allowed under this agreement
        public bool IsTradeAllowed(string goodName, double quantity)
        {
            // Check for embargo
            if (IsEmbargo)
                return false;
                
            // Check for quota
            if (TariffType == TariffType.Quota && !QuotaExemptGoods.Contains(goodName))
            {
                if (ImportQuota > 0 && quantity > ImportQuota)
                    return false;
            }
            
            // Check for prohibited goods
            if (TariffType == TariffType.Prohibitive && GoodSpecificTariffs.ContainsKey(goodName))
                return false;
                
            return true;
        }
        
        // Method to set up an embargo agreement
        public void ConfigureAsEmbargo(string fromCountry, string toCountry, int duration, List<string> demandsMet = null)
        {
            FromCountryName = fromCountry;
            ToCountryName = toCountry;
            TariffType = TariffType.Prohibitive;
            DurationTurns = duration;
            TurnsRemaining = duration;
            Status = TradeStatus.Active;
            IsEmbargo = true;
            TreatyName = $"{fromCountry} Embargo on {toCountry}";
            
            // Set diplomatic demands that would lift the embargo
            if (demandsMet != null)
                EmbargoDemandsMet = demandsMet;
        }
        
        // Method to check if this agreement should be auto-renewed
        public bool ShouldAutoRenew()
        {
            return AutoRenew && TurnsRemaining <= 0;
        }
        
        // Method to renew the agreement for another term
        public void Renew()
        {
            TurnsRemaining = DurationTurns;
            Console.WriteLine($"Trade agreement {TreatyName} has been renewed for {DurationTurns} turns.");
        }
        
        // Method to calculate breaking penalty
        public double GetBreakingPenalty(string countryName)
        {
            if (ParticipatingCountries.Contains(countryName))
                return BreakingPenalty;
            return 0;
        }
    }
    
    public class EnhancedTradeManager : DiplomacyManager
    {
        public List<EnhancedTradeAgreement> EnhancedTradeAgreements { get; private set; }
        
        public EnhancedTradeManager(List<Country> gameCountries) : base(gameCountries)
        {
            EnhancedTradeAgreements = new List<EnhancedTradeAgreement>();
        }
        
        // Method to create a new enhanced trade agreement
        public EnhancedTradeAgreement CreateEnhancedTradeAgreement(string fromCountry, string toCountry, 
                                                                  string resource, double quantity, 
                                                                  double price, int duration,
                                                                  TariffType tariffType, double tariffRate)
        {
            var agreement = new EnhancedTradeAgreement(fromCountry, toCountry, resource, quantity, price, duration)
            {
                TariffType = tariffType,
                TariffRate = tariffRate
            };
            
            EnhancedTradeAgreements.Add(agreement);
            return agreement;
        }
        
        // Method to create a multilateral trade agreement
        public EnhancedTradeAgreement CreateMultilateralAgreement(List<string> countries, string treatyName, 
                                                                 int duration, TariffType tariffType,
                                                                 double tariffRate)
        {
            var agreement = new EnhancedTradeAgreement(countries, treatyName, duration)
            {
                TariffType = tariffType,
                TariffRate = tariffRate
            };
            
            EnhancedTradeAgreements.Add(agreement);
            return agreement;
        }
        
        // Method to impose an embargo
        public EnhancedTradeAgreement ImposeEmbargo(string fromCountry, string toCountry, int duration)
        {
            var embargo = new EnhancedTradeAgreement(fromCountry, toCountry, "", 0, 0, duration);
            embargo.ConfigureAsEmbargo(fromCountry, toCountry, duration);
            
            EnhancedTradeAgreements.Add(embargo);
            
            // Also update diplomatic relations to reflect the embargo
            EstablishDiplomaticRelation(fromCountry, toCountry, DiplomaticStance.TradeEmbargo, duration);
            
            return embargo;
        }
        
        // Method to get all active enhanced agreements for a country
        public List<EnhancedTradeAgreement> GetEnhancedAgreementsForCountry(string countryName)
        {
            return EnhancedTradeAgreements.Where(a => 
                a.ParticipatingCountries.Contains(countryName) && 
                a.Status == TradeStatus.Active).ToList();
        }
        
        // Method to find all active embargoes affecting a country
        public List<EnhancedTradeAgreement> GetEmbargoesAffectingCountry(string countryName)
        {
            return EnhancedTradeAgreements.Where(a => 
                a.IsEmbargo && 
                a.Status == TradeStatus.Active && 
                (a.ToCountryName == countryName || a.ParticipatingCountries.Contains(countryName))).ToList();
        }
        
        // Override the base method to also handle enhanced agreements
        public new void ProcessTurnEnd()
        {
            // First, call the base implementation to handle standard agreements
            base.ProcessTurnEnd();
            
            // Then process enhanced agreements
            foreach (var agreement in EnhancedTradeAgreements)
            {
                if (agreement.Status == TradeStatus.Active)
                {
                    if (agreement.TurnsRemaining > 0)
                    {
                        agreement.TurnsRemaining--;
                        
                        // Check if agreement should auto-renew
                        if (agreement.TurnsRemaining <= 0 && agreement.ShouldAutoRenew())
                        {
                            agreement.Renew();
                        }
                        else if (agreement.TurnsRemaining <= 0)
                        {
                            // Agreement has expired
                            agreement.Status = TradeStatus.Completed;
                            Console.WriteLine($"Enhanced trade agreement {agreement.TreatyName} has expired.");
                            
                            // If it was an embargo, restore normal diplomatic relations if possible
                            if (agreement.IsEmbargo)
                            {
                                // Reset diplomatic stance, unless another embargo is still active
                                var otherEmbargoes = GetEmbargoesAffectingCountry(agreement.ToCountryName)
                                    .Where(e => e.Status == TradeStatus.Active && e != agreement);
                                
                                if (!otherEmbargoes.Any())
                                {
                                    EstablishDiplomaticRelation(agreement.FromCountryName, 
                                                              agreement.ToCountryName, 
                                                              DiplomaticStance.Peace);
                                }
                            }
                        }
                    }
                }
            }
            
            // Process country-to-country general trade modifiers
            // This would adjust the base trade framework to respect tariffs and quotas
        }
        
        // Calculate the effective tariff for a trade between two countries
        public double CalculateEffectiveTariff(string fromCountry, string toCountry, string goodName, 
                                             double quantity, double basePrice)
        {
            double effectiveTariff = 0;
            bool hasAgreement = false;
            
            // Check for direct bilateral agreements first
            foreach (var agreement in GetEnhancedAgreementsForCountry(toCountry))
            {
                if (agreement.ParticipatingCountries.Contains(fromCountry))
                {
                    // Found an agreement between these countries
                    hasAgreement = true;
                    
                    // Get tariff based on this agreement
                    effectiveTariff = agreement.CalculateTariff(goodName, quantity, basePrice);
                    break;
                }
            }
            
            // If no direct agreement, check for most favored nation status
            if (!hasAgreement)
            {
                var mfnAgreements = GetEnhancedAgreementsForCountry(toCountry)
                    .Where(a => a.MutualMostFavoredNation && !a.IsEmbargo)
                    .ToList();
                
                if (mfnAgreements.Any())
                {
                    // Find the lowest tariff rate given to any nation
                    double lowestTariffRate = double.MaxValue;
                    
                    foreach (var agreement in mfnAgreements)
                    {
                        if (agreement.TariffType == TariffType.AdValorem)
                        {
                            lowestTariffRate = Math.Min(lowestTariffRate, agreement.TariffRate);
                        }
                    }
                    
                    if (lowestTariffRate < double.MaxValue)
                    {
                        effectiveTariff = quantity * basePrice * (lowestTariffRate / 100.0);
                    }
                }
            }
            
            // Check for embargoes which would override any other agreements
            var embargoes = GetEmbargoesAffectingCountry(toCountry)
                .Where(e => e.FromCountryName == fromCountry || e.ToCountryName == toCountry)
                .ToList();
                
            if (embargoes.Any())
            {
                // Embargo in effect - prohibitively high tariff
                return quantity * basePrice * 5.0; // 500% value tariff
            }
            
            return effectiveTariff;
        }
    }
    #endregion
}
