using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing; // For Point type

namespace StrategyGame
{
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
}
