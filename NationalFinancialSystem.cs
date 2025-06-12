// NationalFinancialSystem.cs
using System;
using System.Collections.Generic;
using System.Linq;

namespace StrategyGame // Changed from EconomySim to StrategyGame
{
    public class NationalFinancialSystem
    {
        public string CountryId { get; }
        private List<Bond> outstandingBonds;
        public IReadOnlyList<Bond> OutstandingBonds => outstandingBonds.AsReadOnly(); // Public accessor
        private List<TaxPolicy> taxPolicies;
        public IReadOnlyList<TaxPolicy> TaxPolicies => taxPolicies.AsReadOnly(); // Public accessor for tax policies
        private List<Subsidy> activeSubsidies;
        private List<Tariff> activeTariffs;

        // Central Bank related properties
        public decimal BaseInterestRate { get; private set; }
        public decimal MoneySupply { get; private set; }
        public decimal NationalReserves { get; private set; } // e.g., Gold, Foreign Currency
        public CurrencyStandard CurrentCurrencyStandard { get; private set; }
        public float CreditRating { get; private set; } // A score representing creditworthiness
        public decimal DebtToGdpRatio { get; private set; }
        public decimal InflationRate { get; private set; } // Percentage
        public decimal TaxEfficiency { get; private set; } // 0.0 to 1.0

        // Stock Market related (simplified)
        private Dictionary<string, CorporationFinancials> listedCorporations;

        public NationalFinancialSystem(string countryId, decimal initialMoneySupply, decimal initialReserves, CurrencyStandard standard = CurrencyStandard.Fiat)
        {
            CountryId = countryId;
            outstandingBonds = new List<Bond>();
            taxPolicies = new List<TaxPolicy>();
            activeSubsidies = new List<Subsidy>();
            activeTariffs = new List<Tariff>();
            listedCorporations = new Dictionary<string, CorporationFinancials>();

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
        public Bond IssueBond(string ownerId, decimal principal, float interestRate, int maturityYears, BondType type)
        {
            // Calculate risk-adjusted interest rate based on credit rating
            float adjustedInterestRate = interestRate + ((1.0f - CreditRating) * 0.1f);
            
            // Check debt capacity 
            decimal projectedDebtRatio = (GetTotalOutstandingDebt() + principal) / (MoneySupply * 2.0m);
            if (projectedDebtRatio > 1.5m) // Over 150% debt-to-GDP ratio
            {
                throw new InvalidOperationException("Bond issuance would exceed safe debt capacity.");
            }

            var maturityDate = DateTime.Now.AddYears(maturityYears);
            var bond = new Bond(CountryId, ownerId, principal, adjustedInterestRate, DateTime.Now, maturityDate, type);
            
            outstandingBonds.Add(bond);
            MoneySupply += principal; // Money supply increases when government sells bonds
            NationalReserves += principal * 0.1m; // Some bonds proceeds go to reserves
            
            // Update debt ratio
            UpdateDebtToGdpRatio();

            // If debt ratio is getting high, credit rating takes a small hit
            if (DebtToGdpRatio > 0.8m)
            {
                CreditRating = Math.Max(0.1f, CreditRating - 0.01f);
            }

            return bond;
        }

        public decimal GetTotalOutstandingDebt()
        {
            return outstandingBonds.Where(b => !b.IsDefaulted).Sum(b => b.PrincipalAmount);
        }

        public decimal GetAnnualDebtInterestPayment()
        {
            return outstandingBonds.Where(b => !b.IsDefaulted).Sum(b => b.GetAnnualInterestPayment());
        }

        public void ProcessBondMaturity(Guid bondId)
        {
            // Find the bond. Since bonds are structs, we need to be careful with modifications.
            int bondIndex = outstandingBonds.FindIndex(b => b.Id == bondId);
            if (bondIndex == -1) return;

            Bond bond = outstandingBonds[bondIndex];

            if (bond.MaturityDate <= DateTime.Now && !bond.IsDefaulted)
            {
                // Calculate payment due (principal + final interest)
                decimal paymentDue = bond.PrincipalAmount + bond.GetAnnualInterestPayment(); 
                
                // Check country's ability to pay
                if (MoneySupply >= paymentDue && NationalReserves > 0) 
                {
                    // Process payment
                    MoneySupply -= paymentDue; // Reduce money supply
                    NationalReserves -= paymentDue * 0.1m; // Use some reserves
                    outstandingBonds.RemoveAt(bondIndex); // Remove matured bond

                    // Update credit rating positively for successful bond repayment
                    CreditRating = Math.Min(1.0f, CreditRating + 0.02f);

                    // Update country's debt ratio
                    UpdateDebtToGdpRatio();
                }
                else
                {
                    DefaultOnBond(bondId);
                }
            }
        }

        private void UpdateDebtToGdpRatio()
        {
            // Calculate total outstanding debt
            decimal totalDebt = GetTotalOutstandingDebt();
            // Simplified GDP calculation - in a real implementation this would come from economic data
            decimal estimatedGDP = MoneySupply * 2.0m; // Simplified assumption that GDP is roughly 2x money supply
            DebtToGdpRatio = totalDebt / Math.Max(1, estimatedGDP); // Avoid division by zero
        }

        public void DefaultOnBond(Guid bondId)
        {
            int index = outstandingBonds.FindIndex(b => b.Id == bondId);
            if (index != -1)
            {
                Bond bondToDefault = outstandingBonds[index];
                bondToDefault.IsDefaulted = true;
                outstandingBonds[index] = bondToDefault;

                // Severe consequences for defaulting
                CreditRating *= 0.5f; // Drastic reduction in credit rating
                MoneySupply *= 0.9m; // Currency value drops
                BaseInterestRate += 0.05m; // Interest rates spike
                
                // Impact on future borrowing costs is reflected in credit rating
                // The low credit rating will result in higher interest rates on future bonds
                
                // Notify any observers about the default
                // In a full implementation, this would trigger diplomatic penalties
                // and potential economic sanctions
            }
        }
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
            // Inflation effects
            if (InflationRate > 0.1m) // High inflation scenario
            {
                CreditRating = Math.Max(0.1f, CreditRating - 0.005f); // High inflation hurts credit rating
                MoneySupply *= (1.0m + InflationRate); // Money supply naturally expands with inflation
            }

            // Interest rate effects on debt
            if (BaseInterestRate > 0.15m) // High interest rate scenario
            {
                // High rates make debt more expensive
                foreach (var bond in outstandingBonds.Where(b => !b.IsDefaulted))
                {
                    if ((decimal)bond.InterestRate < BaseInterestRate)
                    {
                        AdjustMoneySupply(-bond.PrincipalAmount * 0.01m); // Reduce money supply as debt servicing increases
                    }
                }
            }

            // Reserve effects
            if (NationalReserves < MoneySupply * 0.1m) // Low reserves scenario
            {
                CreditRating = Math.Max(0.1f, CreditRating - 0.01f); // Low reserves hurt credit rating
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

        public decimal CalculateTaxRevenue(decimal totalAssessableIncome, decimal totalCorporateProfits, decimal totalLandValue, decimal totalConsumptionValue)
        {
            decimal totalRevenue = 0;
            foreach (var policy in taxPolicies)
            {
                decimal revenueFromPolicy = 0;
                switch (policy.Type)
                {
                    case TaxType.IncomeTax:
                        // Simplified: apply flat or base rate. Progressive needs POP income data.
                        revenueFromPolicy = totalAssessableIncome * policy.Rate;
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
                    // Tariffs handled separately for now
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

        #region Stock Market (Conceptual)
        public void ListCorporation(CorporationFinancials corp)
        {
            if (!listedCorporations.ContainsKey(corp.CorporationId))
            {
                listedCorporations.Add(corp.CorporationId, corp);
            }
        }

        public void UpdateSharePrice(string corporationId, decimal newPrice)
        {
            if (listedCorporations.TryGetValue(corporationId, out var corp))
            {
                corp.SharePrice = newPrice;
            }
        }
        #endregion

        #region Subsidies and Tariffs
        public void AddSubsidy(Subsidy subsidy)
        {
            activeSubsidies.Add(subsidy);
        }

        public void RemoveSubsidy(Guid subsidyId)
        {
            activeSubsidies.RemoveAll(s => s.Id == subsidyId);
        }

        public decimal GetTotalSubsidyCost()
        {
            // This is simplified. Actual cost depends on industry size/output for percentage subsidies.
            return activeSubsidies.Where(s => !s.IsPercentage).Sum(s => s.AmountOrPercentage);
        }

        public void AddTariff(Tariff tariff)
        {
            activeTariffs.Add(tariff);
        }

        public void RemoveTariff(Guid tariffId)
        {
            activeTariffs.RemoveAll(t => t.Id == tariffId);
        }

        public decimal CalculateTariffRevenueOnTrade(string productOrCategory, decimal tradeValue, bool isImport)
        {
            var applicableTariff = activeTariffs.FirstOrDefault(t => t.ProductOrCategory == productOrCategory && t.IsImportTariff == isImport);
            if (applicableTariff.Id != Guid.Empty) // Check if a tariff was found
            {
                return tradeValue * applicableTariff.Rate;
            }
            return 0;
        }
        #endregion

        public void UpdateFinancialIndicators(decimal currentGdp) // Called periodically
        {
            if (currentGdp > 0)
            {
                DebtToGdpRatio = GetTotalOutstandingDebt() / currentGdp;
            }
            // TODO: Update inflation based on money supply, velocity, output
            // TODO: Update credit rating based on debt, defaults, economic stability
        }
    }
}
