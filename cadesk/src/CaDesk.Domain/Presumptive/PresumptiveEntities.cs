using System;
using System.Collections.Generic;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.Presumptive;

public static class PresumptiveSection
{
    public const string Sec44AD = "44AD";
    public const string Sec44ADA = "44ADA";
    public const string Sec44AE = "44AE";

    public static readonly string[] All = [Sec44AD, Sec44ADA, Sec44AE];
}

public static class PresumptiveStatus
{
    public const string Draft = "draft";
    public const string Computed = "computed";
    public const string Approved = "approved";
    public const string FiledItr4 = "filed_itr4";

    public static readonly string[] All = [Draft, Computed, Approved, FiledItr4];
}

public static class VehicleType
{
    public const string Heavy = "heavy";
    public const string Other = "other";

    public static readonly string[] All = [Heavy, Other];
}

public class PresumptiveTaxComputation : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string FinancialYear { get; set; } = "2024-25";
    public string AssessmentYear { get; set; } = "2025-26";
    public string Section { get; set; } = PresumptiveSection.Sec44AD;
    public string EntityType { get; set; } = "Individual";
    public string BusinessNatureCode { get; set; } = "";
    public string TradeName { get; set; } = "";

    // Section 44AD / 44ADA Turnover & Deemed Profit
    public decimal GrossTurnover { get; set; }
    public decimal DigitalTurnover { get; set; }
    public decimal CashTurnover { get; set; }
    public decimal DigitalPercentage { get; set; }
    public bool IsDigitalThresholdEligible { get; set; }
    public decimal TurnoverLimit { get; set; } = 20_000_000m;
    public bool IsTurnoverWithinLimit { get; set; } = true;
    public decimal DeemedProfitDigitalRate { get; set; } = 6.00m;
    public decimal DeemedProfitCashRate { get; set; } = 8.00m;
    public decimal MinimumDeemedProfit { get; set; }
    public decimal DeclaredProfit { get; set; }

    // Section 44AD(4) 5-Year Lockout Tracking
    public bool OptedOutPriorYear { get; set; }
    public string? OptOutAssessmentYear { get; set; }
    public bool IsLockoutActive { get; set; }
    public string? LockoutExpiryAy { get; set; }

    // Section 44AB Audit Trigger Checks
    public bool IsAuditMandatory { get; set; }
    public string? AuditTriggerReason { get; set; }

    // Section 44AA Minimal Financials (ITR-4 Particulars)
    public decimal SundryDebtors { get; set; }
    public decimal SundryCreditors { get; set; }
    public decimal StockInTrade { get; set; }
    public decimal CashBalance { get; set; }
    public decimal BankBalance { get; set; }
    public decimal FixedAssets { get; set; }
    public decimal OtherAssets { get; set; }
    public decimal PartnerOrProprietorCapital { get; set; }
    public decimal LoansAndBorrowings { get; set; }

    // Status & Lifecycle
    public string Status { get; set; } = PresumptiveStatus.Draft;
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation for 44AE
    public List<Presumptive44AeVehicle> Vehicles { get; set; } = [];
}

public class Presumptive44AeVehicle : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ComputationId { get; set; }
    public PresumptiveTaxComputation? Computation { get; set; }

    public string RegistrationNumber { get; set; } = "";
    public string VehicleType { get; set; } = Presumptive.VehicleType.Other;
    public decimal GrossVehicleWeightTons { get; set; }
    public int MonthsOwned { get; set; } = 12;
    public decimal RatePerMonth { get; set; } = 7500m;
    public decimal DeemedMonthlyProfit { get; set; }
    public decimal DeemedAnnualProfit { get; set; }
    public decimal DeclaredProfit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class PresumptiveTaxEngine
{
    public const decimal Sec44ADStandardLimit = 20_000_000m; // ₹2 Crores
    public const decimal Sec44ADEnhancedLimit = 30_000_000m; // ₹3 Crores (if cash <= 5%)
    public const decimal Sec44ADAStandardLimit = 5_000_000m;  // ₹50 Lakhs
    public const decimal Sec44ADAEnhancedLimit = 7_500_000m;  // ₹75 Lakhs (if cash <= 5%)
    public const decimal Sec44AELightVehicleMonthlyRate = 7500m;
    public const decimal Sec44AEHeavyVehiclePerTonRate = 1000m;
    public const int Sec44AEMaxVehiclesLimit = 10;

    public static string ComputeAssessmentYear(string fy)
    {
        if (string.IsNullOrWhiteSpace(fy)) return "2025-26";
        var parts = fy.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[0], out int startYear))
        {
            int ayStart = startYear + 1;
            int ayEnd = (ayStart + 1) % 100;
            return $"{ayStart}-{ayEnd:D2}";
        }
        return "2025-26";
    }

    public static (
        decimal GrossTurnover,
        decimal DigitalTurnover,
        decimal CashTurnover,
        decimal DigitalPercentage,
        bool IsDigitalThresholdEligible,
        decimal TurnoverLimit,
        bool IsTurnoverWithinLimit,
        decimal MinimumDeemedProfit,
        bool IsAuditMandatory,
        string? AuditReason,
        bool IsLockoutActive,
        string? LockoutExpiryAy
    ) CalculateSection44AD(
        decimal digitalTurnover,
        decimal cashTurnover,
        decimal declaredProfit,
        bool optedOutPriorYear = false,
        string? optOutAy = null,
        string? currentAy = null,
        string? financialYear = null)
    {
        var gross = Math.Max(0, digitalTurnover) + Math.Max(0, cashTurnover);
        var cashPct = gross > 0 ? Math.Round((cashTurnover / gross) * 100m, 2) : 0m;
        var digitalPct = gross > 0 ? Math.Round((digitalTurnover / gross) * 100m, 2) : 100m;

        // Finance Act 2023 amendment: enhanced limit of ₹3 Cr if cash receipts <= 5%
        var isDigitalEligible = gross > 0 && cashPct <= 5.0m;
        var limit = isDigitalEligible ? Sec44ADEnhancedLimit : Sec44ADStandardLimit;
        var isWithinLimit = gross <= limit;

        // Minimum deemed profit: 6% on digital, 8% on non-digital/cash
        var digitalProfit = Math.Round(Math.Max(0, digitalTurnover) * 0.06m, 2);
        var cashProfit = Math.Round(Math.Max(0, cashTurnover) * 0.08m, 2);
        var minProfit = digitalProfit + cashProfit;

        // Evaluate Section 44AD(4) 5-year lockout
        var (isLockout, expiryAy) = EvaluateLockout(optedOutPriorYear, optOutAy, currentAy);

        // Audit requirement evaluation
        var (isAudit, reason) = Evaluate44ABAudit(
            section: PresumptiveSection.Sec44AD,
            grossTurnover: gross,
            turnoverLimit: limit,
            isWithinLimit: isWithinLimit,
            minimumDeemedProfit: minProfit,
            declaredProfit: declaredProfit,
            isLockoutActive: isLockout,
            financialYear: financialYear
        );

        return (
            gross,
            digitalTurnover,
            cashTurnover,
            digitalPct,
            isDigitalEligible,
            limit,
            isWithinLimit,
            minProfit,
            isAudit,
            reason,
            isLockout,
            expiryAy
        );
    }

    public static (
        decimal GrossReceipts,
        decimal DigitalReceipts,
        decimal CashReceipts,
        decimal DigitalPercentage,
        bool IsDigitalThresholdEligible,
        decimal ReceiptsLimit,
        bool IsWithinLimit,
        decimal MinimumDeemedProfit,
        bool IsAuditMandatory,
        string? AuditReason
    ) CalculateSection44ADA(
        decimal digitalReceipts,
        decimal cashReceipts,
        decimal declaredProfit,
        string? financialYear = null)
    {
        var gross = Math.Max(0, digitalReceipts) + Math.Max(0, cashReceipts);
        var cashPct = gross > 0 ? Math.Round((cashReceipts / gross) * 100m, 2) : 0m;
        var digitalPct = gross > 0 ? Math.Round((digitalReceipts / gross) * 100m, 2) : 100m;

        // Finance Act 2023: ₹75 Lakhs if cash <= 5%, otherwise ₹50 Lakhs
        var isDigitalEligible = gross > 0 && cashPct <= 5.0m;
        var limit = isDigitalEligible ? Sec44ADAEnhancedLimit : Sec44ADAStandardLimit;
        var isWithinLimit = gross <= limit;

        // 50% minimum deemed profit on total gross receipts
        var minProfit = Math.Round(gross * 0.50m, 2);

        var (isAudit, reason) = Evaluate44ABAudit(
            section: PresumptiveSection.Sec44ADA,
            grossTurnover: gross,
            turnoverLimit: limit,
            isWithinLimit: isWithinLimit,
            minimumDeemedProfit: minProfit,
            declaredProfit: declaredProfit,
            isLockoutActive: false,
            financialYear: financialYear
        );

        return (
            gross,
            digitalReceipts,
            cashReceipts,
            digitalPct,
            isDigitalEligible,
            limit,
            isWithinLimit,
            minProfit,
            isAudit,
            reason
        );
    }

    public static (
        decimal MonthlyRate,
        decimal DeemedMonthlyProfit,
        decimal DeemedAnnualProfit
    ) CalculateSection44AeVehicle(
        string vehicleType,
        decimal grossVehicleWeightTons,
        int monthsOwned)
    {
        var months = Math.Clamp(monthsOwned, 1, 12);
        decimal monthlyRate;

        if (vehicleType.Equals(VehicleType.Heavy, StringComparison.OrdinalIgnoreCase) || grossVehicleWeightTons > 12.0m)
        {
            // Section 44AE(2)(i): ₹1,000 per ton of gross vehicle weight for heavy goods carriage
            monthlyRate = Math.Max(12.0m, grossVehicleWeightTons) * Sec44AEHeavyVehiclePerTonRate;
        }
        else
        {
            // Section 44AE(2)(ii): ₹7,500 per month for other goods carriage
            monthlyRate = Sec44AELightVehicleMonthlyRate;
        }

        var annualProfit = monthlyRate * months;
        return (monthlyRate, monthlyRate, annualProfit);
    }

    public static (bool IsLockoutActive, string? ExpiryAy) EvaluateLockout(
        bool optedOutPriorYear,
        string? optOutAy,
        string? currentAy)
    {
        if (!optedOutPriorYear || string.IsNullOrWhiteSpace(optOutAy))
            return (false, null);

        var partsOptOut = optOutAy.Split('-');
        if (partsOptOut.Length != 2 || !int.TryParse(partsOptOut[0], out int optOutStartYear))
            return (false, null);

        // 5 consecutive Assessment Years following the opt-out AY
        int expiryStartYear = optOutStartYear + 5;
        int expiryEndYear = (expiryStartYear + 1) % 100;
        var expiryAy = $"{expiryStartYear}-{expiryEndYear:D2}";

        if (string.IsNullOrWhiteSpace(currentAy))
            return (true, expiryAy);

        var partsCurr = currentAy.Split('-');
        if (partsCurr.Length == 2 && int.TryParse(partsCurr[0], out int currentStartYear))
        {
            bool isActive = currentStartYear <= expiryStartYear;
            return (isActive, expiryAy);
        }

        return (true, expiryAy);
    }

    public static (bool IsAuditMandatory, string? Reason) Evaluate44ABAudit(
        string section,
        decimal grossTurnover,
        decimal turnoverLimit,
        bool isWithinLimit,
        decimal minimumDeemedProfit,
        decimal declaredProfit,
        bool isLockoutActive,
        string? financialYear = null)
    {
        // cite the law of the year (Income-tax Act, 2025 from tax year 2026-27)
        string S(string ref1961) => IncomeTaxLaw.Sec(ref1961, financialYear);

        if (!isWithinLimit)
        {
            return (true, $"Turnover/Receipts ({grossTurnover:N0}) exceeds the statutory presumptive ceiling of {turnoverLimit:N0} under {S(section)}. Mandatory Tax Audit under {S("44AB")}.");
        }

        if (isLockoutActive)
        {
            return (true, $"Assessee is under active 5-year {S("44AD(4)")} penalty lockout for previously declaring profit below statutory rate. Mandatory Tax Audit under {S("44AB(e)")} applies.");
        }

        if (declaredProfit > 0 && declaredProfit < minimumDeemedProfit)
        {
            return (true, $"Declared profit ({declaredProfit:N0}) is lower than the statutory minimum deemed profit ({minimumDeemedProfit:N0}). Books of accounts required under {S("44AA")} and Tax Audit mandatory under {S("44AB(c)")}.");
        }

        return (false, null);
    }

    public static (
        decimal TotalAssets,
        decimal TotalLiabilitiesAndCapital,
        decimal Difference,
        bool IsBalanced,
        List<string> ValidationAlerts
    ) ValidateMinimalFinancials(
        decimal sundryDebtors,
        decimal sundryCreditors,
        decimal stockInTrade,
        decimal cashBalance,
        decimal bankBalance,
        decimal fixedAssets,
        decimal otherAssets,
        decimal partnerOrProprietorCapital,
        decimal loansAndBorrowings)
    {
        var alerts = new List<string>();

        if (sundryDebtors < 0) alerts.Add("Sundry Debtors cannot be negative.");
        if (sundryCreditors < 0) alerts.Add("Sundry Creditors cannot be negative.");
        if (stockInTrade < 0) alerts.Add("Stock-in-trade cannot be negative.");
        if (cashBalance < 0) alerts.Add("Cash balance cannot be negative (Cash in hand cannot drop below zero).");

        var totalAssets = sundryDebtors + stockInTrade + cashBalance + bankBalance + fixedAssets + otherAssets;
        var totalLiabAndCap = sundryCreditors + loansAndBorrowings + partnerOrProprietorCapital;
        var diff = Math.Abs(totalAssets - totalLiabAndCap);
        var isBalanced = diff <= 1.0m;

        if (!isBalanced && (totalAssets > 0 || totalLiabAndCap > 0))
        {
            alerts.Add($"Balance Sheet variance of ₹{diff:N2}. Total Assets (₹{totalAssets:N2}) does not equal Total Capital & Liabilities (₹{totalLiabAndCap:N2}).");
        }

        return (totalAssets, totalLiabAndCap, diff, isBalanced, alerts);
    }
}
