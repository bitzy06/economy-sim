// FinancialSystemCore.cs
using System;
using System.Collections.Generic;

namespace StrategyGame // Changed from EconomySim to StrategyGame
{
    public enum BondType
    {
        TreasuryBill, // Short-term
        GovernmentBond // Long-term
    }

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
        Tariff,
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

    public struct Bond
    {
        public Guid Id { get; }
        public string IssuerCountryId { get; } // ID of the issuing country
        public string OwnerId { get; set; } // Can be another country, a corporation, a POP group, or central bank
        public decimal PrincipalAmount { get; }
        public float InterestRate { get; } // Annual coupon rate
        public DateTime IssueDate { get; }
        public DateTime MaturityDate { get; }
        public BondType Type { get; }
        public bool IsDefaulted { get; set; }

        public Bond(string issuerCountryId, string ownerId, decimal principalAmount, float interestRate, DateTime issueDate, DateTime maturityDate, BondType type)
        {
            Id = Guid.NewGuid();
            IssuerCountryId = issuerCountryId;
            OwnerId = ownerId;
            PrincipalAmount = principalAmount;
            InterestRate = interestRate;
            IssueDate = issueDate;
            MaturityDate = maturityDate;
            Type = type;
            IsDefaulted = false;
        }

        public decimal GetAnnualInterestPayment()
        {
            return PrincipalAmount * (decimal)InterestRate;
        }
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

    public class CorporationFinancials
    {
        public string CorporationId { get; }
        public long TotalShares { get; set; }
        public long PubliclyTradedShares { get; set; }
        public decimal SharePrice { get; set; }
        public decimal LastDividendPerShare { get; set; }
        public Dictionary<string, long> ShareHolders { get; set; } // OwnerId -> Number of shares

        public CorporationFinancials(string corporationId, long totalShares, decimal initialSharePrice)
        {
            CorporationId = corporationId;
            TotalShares = totalShares;
            PubliclyTradedShares = 0; // Initially private or fully owned
            SharePrice = initialSharePrice;
            LastDividendPerShare = 0;
            ShareHolders = new Dictionary<string, long>();
        }
    }

    public class Subsidy
    {
        public Guid Id { get; }
        public string TargetIndustryOrProduct { get; set; }
        public decimal AmountOrPercentage { get; set; } // Can be a fixed amount or a percentage of costs/revenue
        public bool IsPercentage { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; } // Optional: for temporary subsidies

        public Subsidy(string target, decimal value, bool isPercentage, DateTime startDate, DateTime? endDate = null)
        {
            Id = Guid.NewGuid();
            TargetIndustryOrProduct = target;
            AmountOrPercentage = value;
            IsPercentage = isPercentage;
            StartDate = startDate;
            EndDate = endDate;
        }
    }

    public class Tariff
    {
        public Guid Id { get; }
        public string ProductOrCategory { get; set; } // Specific product ID or a broader category
        public decimal Rate { get; set; } // Percentage
        public bool IsImportTariff { get; set; } // True for import, false for export
        
        public Tariff(string productOrCategory, decimal rate, bool isImportTariff)
        {
            Id = Guid.NewGuid();
            ProductOrCategory = productOrCategory;
            Rate = rate;
            IsImportTariff = isImportTariff;
        }
    }
}
