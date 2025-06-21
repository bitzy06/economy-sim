// NationalFinancialSystem.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace StrategyGame // Changed from EconomySim to StrategyGame
{
    // Core finance enums and structures consolidated here
    public enum CurrencyStandard
    {
        Fiat,
        GoldBacked,
        SilverBacked
    }

    public enum TaxType
    {
        IncomeTax,
        CorporateTax,
        LandTax,
        ConsumptionTax, // Sales Tax / VAT
        PollTax
    }

    public enum TaxProgressivity
    {
        Progressive,
        Flat,
        Regressive
    }

    public struct TaxPolicy
    {
        public Guid Id { get; }
        public TaxType Type { get; set; }
        public decimal Rate { get; set; } // Flat rate or base rate for progressive
        public TaxProgressivity Progressivity { get; set; } // For income tax mainly
        public Dictionary<decimal, decimal> ProgressiveBrackets { get; set; } // Income threshold -> Rate for that bracket
        public string AppliesToSector { get; set; } // Optional: for sector-specific taxes/breaks
        public string AppliesToPopGroup { get; set; } // Optional: for POP group specific taxes

        public TaxPolicy(TaxType type, decimal rate, TaxProgressivity progressivity = TaxProgressivity.Flat)
        {
            Id = Guid.NewGuid();
            Type = type;
            Rate = rate;
            Progressivity = progressivity;
            ProgressiveBrackets = new Dictionary<decimal, decimal>();
            AppliesToSector = null;
            AppliesToPopGroup = null;
        }
    }

    public class NationalFinancialSystem
    {
        public string CountryId { get; }
        // Bond system removed
        private List<TaxPolicy> taxPolicies;
        public IReadOnlyList<TaxPolicy> TaxPolicies => taxPolicies.AsReadOnly(); // Public accessor for tax policies

        // Simplified system: subsidies, tariffs, and stock market removed
        // Central Bank related properties
        public decimal BaseInterestRate { get; private set; }
        public decimal MoneySupply { get; private set; }
        public decimal NationalReserves { get; private set; } // e.g., Gold, Foreign Currency
        public CurrencyStandard CurrentCurrencyStandard { get; private set; }
        public float CreditRating { get; private set; } // A score representing creditworthiness
        public decimal DebtToGdpRatio { get; private set; }
        public decimal InflationRate { get; private set; } // Percentage
        public decimal TaxEfficiency { get; private set; } // 0.0 to 1.0


        public NationalFinancialSystem(string countryId, decimal initialMoneySupply, decimal initialReserves, CurrencyStandard standard = CurrencyStandard.Fiat)
        {
            CountryId = countryId;
            taxPolicies = new List<TaxPolicy>();

            MoneySupply = initialMoneySupply;
            NationalReserves = initialReserves;
            CurrentCurrencyStandard = standard;
            BaseInterestRate = 0.05m; // Default 5%
            CreditRating = 0.75f; // Default good rating
            DebtToGdpRatio = 0.0m;
            InflationRate = 0.02m; // Default 2%
            TaxEfficiency = 0.85m; // Default 85%
        }

        #region Debt and Bonds
        // Bond mechanics removed for now. The game no longer models national debt via
        // individual bond instruments, so these methods have been trimmed.

        public decimal GetTotalOutstandingDebt() => 0m;

        public decimal GetAnnualDebtInterestPayment() => 0m;

        public void ProcessBondMaturity(Guid bondId) { }

        private void UpdateDebtToGdpRatio() => DebtToGdpRatio = 0m;

        public void DefaultOnBond(Guid bondId) { }
        #endregion

        #region Central Bank
        public void SetBaseInterestRate(decimal newRate)
        {
            if (newRate < 0) throw new ArgumentException("Interest rate cannot be negative");
            
            decimal oldRate = BaseInterestRate;
            BaseInterestRate = newRate;

            // Impact on money supply and inflation
            if (newRate > oldRate)
            {
                // Higher rates reduce money supply and inflation
                MoneySupply *= (1.0m - ((newRate - oldRate) * 0.1m)); // Reduce money supply proportionally
                InflationRate = Math.Max(0, InflationRate - ((newRate - oldRate) * 0.5m)); // Reduce inflation
            }
            else
            {
                // Lower rates increase money supply and may increase inflation
                MoneySupply *= (1.0m + ((oldRate - newRate) * 0.15m)); // Increase money supply
                InflationRate = Math.Min(0.5m, InflationRate + ((oldRate - newRate) * 0.3m)); // Increase inflation risk
            }
        }

        public void AdjustMoneySupply(decimal amount)
        {
            // Positive amount = print money, negative = remove from circulation
            MoneySupply = Math.Max(0, MoneySupply + amount);
            
            // Impact on inflation
            if (amount > 0)
            {
                // Increasing money supply risks higher inflation
                decimal inflationImpact = (amount / MoneySupply) * 0.5m;
                InflationRate = Math.Min(0.5m, InflationRate + inflationImpact);
            }
            else
            {
                // Reducing money supply helps control inflation
                decimal inflationReduction = (-amount / MoneySupply) * 0.3m;
                InflationRate = Math.Max(0, InflationRate - inflationReduction);
            }

            // Update debt ratio as money supply changes
            UpdateDebtToGdpRatio();
        }

        public void AdjustReserves(decimal amount)
        {
            decimal oldReserves = NationalReserves;
            NationalReserves = Math.Max(0, NationalReserves + amount);

            // Impact on credit rating
            if (NationalReserves > oldReserves)
            {
                CreditRating = Math.Min(1.0f, CreditRating + 0.01f);
            }
            else if (NationalReserves < oldReserves)
            {
                CreditRating = Math.Max(0.1f, CreditRating - 0.01f);
            }
        }

        // Simulate the impact of current monetary policy and economic conditions
        public void SimulateMonetaryEffects()
        {
            // Money supply changes each tick based on current inflation
            MoneySupply *= (1.0m + InflationRate);

            // Inflation effects on credit rating when it gets high
            if (InflationRate > 0.1m) // High inflation scenario
            {
                CreditRating = Math.Max(0.1f, CreditRating - 0.005f); // High inflation hurts credit rating
            }

            // Interest rate effects no longer account for bonds since the bond system was removed

            // Reserve effects
            if (NationalReserves < MoneySupply * 0.1m) // Low reserves scenario
            {
                CreditRating = Math.Max(0.1f, CreditRating - 0.01f); // Low reserves hurt credit rating
            }
            else
            {
                // Very small passive increase to represent investment returns
                NationalReserves *= 1.001m;
            }
        }

        public void SetCurrencyStandard(CurrencyStandard standard)
        {
            CurrentCurrencyStandard = standard;
            // TODO: Impact on currency stability and policy flexibility
        }
        #endregion

        #region Taxation
        public void AddTaxPolicy(TaxPolicy policy)
        {
            taxPolicies.Add(policy);
        }

        public void RemoveTaxPolicy(Guid policyId)
        {
            taxPolicies.RemoveAll(p => p.Id == policyId);
        }

        public decimal CalculateTaxRevenue(Dictionary<string, decimal> popGroupIncome,
                                           decimal totalCorporateProfits,
                                           decimal totalLandValue,
                                           decimal totalConsumptionValue,
                                           int totalPopulation)
        {
            decimal totalRevenue = 0;

            foreach (var policy in taxPolicies)
            {
                decimal revenueFromPolicy = 0;

                switch (policy.Type)
                {
                    case TaxType.IncomeTax:
                        decimal applicableIncome = 0;
                        if (!string.IsNullOrEmpty(policy.AppliesToPopGroup))
                        {
                            if (popGroupIncome.TryGetValue(policy.AppliesToPopGroup, out var groupInc))
                                applicableIncome = groupInc;
                        }
                        else
                        {
                            foreach (var val in popGroupIncome.Values) applicableIncome += val;
                        }

                        if (policy.Progressivity == TaxProgressivity.Progressive && policy.ProgressiveBrackets.Count > 0)
                        {
                            decimal remaining = applicableIncome;
                            decimal progressiveRevenue = 0;
                            decimal previous = 0;
                            foreach (var bracket in policy.ProgressiveBrackets.OrderBy(b => b.Key))
                            {
                                decimal taxable = Math.Max(0, Math.Min(remaining, bracket.Key - previous));
                                progressiveRevenue += taxable * bracket.Value;
                                remaining -= taxable;
                                previous = bracket.Key;
                                if (remaining <= 0) break;
                            }
                            if (remaining > 0)
                                progressiveRevenue += remaining * policy.Rate;

                            revenueFromPolicy = progressiveRevenue;
                        }
                        else
                        {
                            revenueFromPolicy = applicableIncome * policy.Rate;
                        }
                        break;

                    case TaxType.CorporateTax:
                        revenueFromPolicy = totalCorporateProfits * policy.Rate;
                        break;

                    case TaxType.LandTax:
                        revenueFromPolicy = totalLandValue * policy.Rate;
                        break;

                    case TaxType.ConsumptionTax:
                        revenueFromPolicy = totalConsumptionValue * policy.Rate;
                        break;

                    case TaxType.PollTax:
                        revenueFromPolicy = totalPopulation * policy.Rate;
                        break;
                }

                totalRevenue += revenueFromPolicy;
            }

            return totalRevenue * TaxEfficiency;
        }

        public void SetTaxEfficiency(decimal efficiency)
        {
            TaxEfficiency = Clamp(efficiency, 0.0m, 1.0m);
        }

        private decimal Clamp(decimal value, decimal min, decimal max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
        #endregion

        
        
        public void UpdateFinancialIndicators(decimal currentGdp) // Called periodically
        {
            if (currentGdp > 0)
            {
                DebtToGdpRatio = GetTotalOutstandingDebt() / currentGdp;

                // Simple inflation model based on money supply relative to GDP
                decimal moneyRatio = MoneySupply / currentGdp;
                InflationRate = Clamp(0.02m + (moneyRatio - 1m) * 0.02m, 0m, 0.5m);

                // Adjust credit rating based on debt burden
                if (DebtToGdpRatio > 1m)
                    CreditRating = Math.Max(0.1f, CreditRating - 0.01f);
                else if (DebtToGdpRatio < 0.5m)
                    CreditRating = Math.Min(1.0f, CreditRating + 0.005f);
            }
        }
    }
}
