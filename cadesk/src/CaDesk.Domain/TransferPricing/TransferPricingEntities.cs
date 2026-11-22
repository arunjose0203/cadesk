using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.TransferPricing;

public static class TransferPricingConstants
{
    // Transaction Categories
    public const string CatInternational = "InternationalTransaction";
    public const string CatSpecifiedDomestic = "SpecifiedDomesticTransaction";
    public const string CatDeemedInternational = "DeemedInternationalTransaction";

    // Most Appropriate Methods (Rule 10B & Rule 10AB)
    public const string MethodCup = "CUP";         // Comparable Uncontrolled Price
    public const string MethodRpm = "RPM";         // Resale Price Method
    public const string MethodCpm = "CPM";         // Cost Plus Method
    public const string MethodPsm = "PSM";         // Profit Split Method
    public const string MethodTnmm = "TNMM";       // Transactional Net Margin Method
    public const string MethodOther = "OtherMethod"; // Rule 10AB

    // Profit Level Indicators (PLI)
    public const string PliOpTc = "OP_TC";         // Operating Profit / Total Cost (Mark-up on Cost)
    public const string PliOpOr = "OP_OR";         // Operating Profit / Operating Revenue (Operating Margin)
    public const string PliGpSales = "GP_Sales";   // Gross Profit / Sales (Resale margin)
    public const string PliGpCost = "GP_Cost";     // Gross Profit / Cost (Cost plus mark-up)
    public const string PliRoce = "ROCE";          // Return on Capital Employed
    public const string PliUnitPrice = "UnitPrice";// Unit Price (CUP)

    // Study Statuses
    public const string StatusDraft = "Draft";
    public const string StatusBenchmarking = "BenchmarkingInProgress";
    public const string StatusUnderReview = "UnderReview";
    public const string StatusCertified = "Certified";

    // Standard Tolerance Bands (Rule 10CA)
    public const decimal DefaultToleranceBandPct = 3.0m; // 3%
    public const decimal WholesaleToleranceBandPct = 1.0m; // 1% for wholesale trading

    // Specified Domestic Transaction Threshold (Section 92BA)
    public const decimal SdtStatutoryThreshold = 20_00_00_000m; // ₹20 Crore
}

public class TransferPricingStudy : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string StudyReference { get; set; } = "";
    public string Title { get; set; } = "";
    public string FinancialYear { get; set; } = ""; // e.g., "2024-25"
    public string AssessmentYear { get; set; } = ""; // e.g., "2025-26"
    public DateTime AuditPeriodStart { get; set; }
    public DateTime AuditPeriodEnd { get; set; }

    // Rule 10D Local File compliance flag
    public bool Rule10DDocumentationMaintained { get; set; } = true;
    public string Rule10DDocumentationPath { get; set; } = "";

    // Study state & Auditor details
    public string StudyStatus { get; set; } = TransferPricingConstants.StatusDraft;
    public string AccountantName { get; set; } = "";
    public string AccountantFirmName { get; set; } = "";
    public string MembershipNumber { get; set; } = "";
    public string FirmRegistrationNumber { get; set; } = "";

    // Certification & UDIN (18 alphanumeric chars)
    public string? Udin { get; set; }
    public DateTime? CertifiedAt { get; set; }
    public string? CertifiedBy { get; set; }

    // Aggregate statistics
    public int TotalAssociatedEnterprisesCount { get; set; }
    public int TotalInternationalTransactionsCount { get; set; }
    public decimal TotalInternationalInrValue { get; set; }

    public int TotalSdtTransactionsCount { get; set; }
    public decimal TotalSdtInrValue { get; set; }

    public int TotalAlpAdjustmentsCount { get; set; }
    public decimal TotalAlpAdjustmentAmount { get; set; } // Primary adjustment to income

    public int TransactionsWithinToleranceCount { get; set; }
    public int TransactionsOutsideToleranceCount { get; set; }

    // Generated Statutory Form 3CEB
    public string? Form3CebReportText { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation collections
    public List<AssociatedEnterprise> AssociatedEnterprises { get; set; } = [];
    public List<TransferPricingTransaction> Transactions { get; set; } = [];
    public List<ComparableBenchmark> Comparables { get; set; } = [];
}

public class AssociatedEnterprise : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid StudyId { get; set; }
    public TransferPricingStudy? Study { get; set; }

    public string EnterpriseName { get; set; } = "";
    public string CountryOfResidence { get; set; } = "USA";
    public string CountryCode { get; set; } = "US";
    public string TaxIdentificationNumber { get; set; } = ""; // EIN, SSN, TIN
    public string RegisteredAddress { get; set; } = "";
    public string RelationshipSection { get; set; } = "Sec 92A(2)(a) - Voting Power >= 26%";
    public decimal OwnershipPercentage { get; set; }
    public string BriefActivityDescription { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TransferPricingTransaction : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid StudyId { get; set; }
    public TransferPricingStudy? Study { get; set; }

    public Guid? AssociatedEnterpriseId { get; set; }
    public AssociatedEnterprise? AssociatedEnterprise { get; set; }

    public string TransactionCategory { get; set; } = TransferPricingConstants.CatInternational;
    public int ClauseNumber { get; set; } = 11; // 11 to 25 under Form 3CEB
    public string DescriptionOfTransaction { get; set; } = "";

    // Transaction Values
    public string CurrencyCode { get; set; } = "USD";
    public decimal ForeignCurrencyAmount { get; set; }
    public decimal ExchangeRate { get; set; } = 1.0m;
    public decimal InrValue { get; set; }

    // Benchmarking & Arm's Length Determination
    public string MostAppropriateMethod { get; set; } = TransferPricingConstants.MethodTnmm;
    public string ProfitLevelIndicator { get; set; } = TransferPricingConstants.PliOpTc;
    public decimal TestedPartyCostBase { get; set; } // Total Cost or Operating Revenue base for calculation
    public decimal TestedPartyMargin { get; set; }   // Actual margin earned/paid (e.g. 15.00%)

    // Benchmark Results (from comparables)
    public int ComparablesCount { get; set; }
    public decimal BenchmarkRangeMin { get; set; }   // 35th percentile or (Mean - 3%)
    public decimal BenchmarkRangeMax { get; set; }   // 65th percentile or (Mean + 3%)
    public decimal BenchmarkMedianOrMean { get; set; } // 50th percentile (Median) or Arithmetic Mean
    public decimal ToleranceBandPercentage { get; set; } = TransferPricingConstants.DefaultToleranceBandPct;

    public bool IsWithinArmLength { get; set; } = true;
    public decimal AlpAdjustmentAmount { get; set; } // Primary adjustment to taxable profit

    public string MethodSelectionRationale { get; set; } = "";
    public string DocumentationReference { get; set; } = "Rule 10D TP Documentation Study Section 5";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class ComparableBenchmark : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid StudyId { get; set; }
    public TransferPricingStudy? Study { get; set; }

    public Guid? TransactionId { get; set; }
    public TransferPricingTransaction? Transaction { get; set; }

    public string ComparableCompanyName { get; set; } = "";
    public string CinOrIdentifier { get; set; } = "";
    public string FinancialYear { get; set; } = "";

    public decimal OperatingRevenue { get; set; }
    public decimal OperatingCost { get; set; }
    public decimal OperatingProfit { get; set; }
    public decimal CalculatedMarginPercentage { get; set; } // OP/TC or OP/OR

    public decimal Weight { get; set; } = 1.0m;
    public bool IsAccepted { get; set; } = true;
    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class TransferPricingEngine
{
    public record RangeResult(
        bool UseRange,
        int DataCount,
        decimal RangeMin,
        decimal RangeMax,
        decimal MedianOrMean,
        string Explanation);

    /// <summary>
    /// Computes the 35th to 65th percentile range (Rule 10CA) if dataset >= 6,
    /// or Arithmetic Mean with 3% tolerance if dataset < 6.
    /// </summary>
    public static RangeResult CalculateArmLengthRange(List<decimal> rawMargins, decimal tolerancePct = TransferPricingConstants.DefaultToleranceBandPct)
    {
        if (rawMargins == null || rawMargins.Count == 0)
        {
            return new RangeResult(false, 0, 0, 0, 0, "No comparable data points available");
        }

        var sorted = rawMargins.OrderBy(x => x).ToList();
        int n = sorted.Count;

        if (n >= 6)
        {
            // Rule 10CA Range Concept:
            // 35th percentile:
            decimal idx35 = n * 0.35m;
            decimal p35;
            if (idx35 == Math.Floor(idx35))
            {
                int i = (int)idx35;
                p35 = (sorted[i - 1] + sorted[i]) / 2m;
            }
            else
            {
                int i = (int)Math.Ceiling(idx35);
                p35 = sorted[i - 1];
            }

            // 65th percentile:
            decimal idx65 = n * 0.65m;
            decimal p65;
            if (idx65 == Math.Floor(idx65))
            {
                int i = (int)idx65;
                p65 = (sorted[i - 1] + sorted[i]) / 2m;
            }
            else
            {
                int i = (int)Math.Ceiling(idx65);
                p65 = sorted[i - 1];
            }

            // 50th percentile (Median):
            decimal idx50 = n * 0.50m;
            decimal p50;
            if (idx50 == Math.Floor(idx50))
            {
                int i = (int)idx50;
                p50 = (sorted[i - 1] + sorted[i]) / 2m;
            }
            else
            {
                int i = (int)Math.Ceiling(idx50);
                p50 = sorted[i - 1];
            }

            return new RangeResult(
                UseRange: true,
                DataCount: n,
                RangeMin: Math.Round(p35, 2),
                RangeMax: Math.Round(p65, 2),
                MedianOrMean: Math.Round(p50, 2),
                Explanation: $"Dataset has {n} comparables (>= 6): Rule 10CA 35th percentile ({p35:N2}%) to 65th percentile ({p65:N2}%) range applies with Median ({p50:N2}%)");
        }
        else
        {
            // Arithmetic mean with tolerance band
            decimal mean = Math.Round(sorted.Average(), 2);
            decimal min = Math.Round(mean * (1m - (tolerancePct / 100m)), 2);
            decimal max = Math.Round(mean * (1m + (tolerancePct / 100m)), 2);

            return new RangeResult(
                UseRange: false,
                DataCount: n,
                RangeMin: min,
                RangeMax: max,
                MedianOrMean: mean,
                Explanation: $"Dataset has {n} comparables (< 6): Arithmetic Mean ({mean:N2}%) applies with +/- {tolerancePct:N1}% tolerance band ({min:N2}% to {max:N2}%)");
        }
    }

    /// <summary>
    /// Evaluates if a transaction is at Arm's Length and calculates the primary TP adjustment if deficient.
    /// </summary>
    public static void EvaluateTransaction(TransferPricingTransaction tx, List<decimal> comparableMargins)
    {
        tx.InrValue = Math.Round(tx.ForeignCurrencyAmount * tx.ExchangeRate, 2);

        if (comparableMargins == null || comparableMargins.Count == 0)
        {
            tx.IsWithinArmLength = true;
            tx.AlpAdjustmentAmount = 0m;
            return;
        }

        var range = CalculateArmLengthRange(comparableMargins, tx.ToleranceBandPercentage);
        tx.ComparablesCount = range.DataCount;
        tx.BenchmarkRangeMin = range.RangeMin;
        tx.BenchmarkRangeMax = range.RangeMax;
        tx.BenchmarkMedianOrMean = range.MedianOrMean;

        // Check if tested party margin is within range
        if (range.UseRange)
        {
            // Range concept (Rule 10CA(3)):
            // If actual margin falls between 35th and 65th percentile, it is at arm's length.
            if (tx.TestedPartyMargin >= range.RangeMin && tx.TestedPartyMargin <= range.RangeMax)
            {
                tx.IsWithinArmLength = true;
                tx.AlpAdjustmentAmount = 0m;
            }
            else if (tx.TestedPartyMargin < range.RangeMin)
            {
                // Deficient margin: Adjustment is calculated with reference to the MEDIAN (50th percentile)
                tx.IsWithinArmLength = false;
                decimal marginGap = range.MedianOrMean - tx.TestedPartyMargin;
                decimal baseAmount = tx.TestedPartyCostBase > 0 ? tx.TestedPartyCostBase : tx.InrValue;
                tx.AlpAdjustmentAmount = Math.Max(0m, Math.Round(baseAmount * (marginGap / 100m), 2));
            }
            else
            {
                // Tested margin is higher than range (more profit earned in India): No adjustment required
                tx.IsWithinArmLength = true;
                tx.AlpAdjustmentAmount = 0m;
            }
        }
        else
        {
            // Arithmetic mean with tolerance band:
            // If actual margin is within +/- 3% of arithmetic mean or above
            if (tx.TestedPartyMargin >= range.RangeMin)
            {
                tx.IsWithinArmLength = true;
                tx.AlpAdjustmentAmount = 0m;
            }
            else
            {
                // Below tolerance minimum: Adjustment made to the arithmetic mean
                tx.IsWithinArmLength = false;
                decimal marginGap = range.MedianOrMean - tx.TestedPartyMargin;
                decimal baseAmount = tx.TestedPartyCostBase > 0 ? tx.TestedPartyCostBase : tx.InrValue;
                tx.AlpAdjustmentAmount = Math.Max(0m, Math.Round(baseAmount * (marginGap / 100m), 2));
            }
        }
    }

    /// <summary>
    /// Recalculates study-level aggregate statistics.
    /// </summary>
    public static void RecalculateStudySummary(TransferPricingStudy study)
    {
        study.TotalAssociatedEnterprisesCount = study.AssociatedEnterprises.Count;

        var intlTxs = study.Transactions.Where(x => x.TransactionCategory == TransferPricingConstants.CatInternational || x.TransactionCategory == TransferPricingConstants.CatDeemedInternational).ToList();
        study.TotalInternationalTransactionsCount = intlTxs.Count;
        study.TotalInternationalInrValue = intlTxs.Sum(x => x.InrValue);

        var sdtTxs = study.Transactions.Where(x => x.TransactionCategory == TransferPricingConstants.CatSpecifiedDomestic).ToList();
        study.TotalSdtTransactionsCount = sdtTxs.Count;
        study.TotalSdtInrValue = sdtTxs.Sum(x => x.InrValue);

        study.TotalAlpAdjustmentsCount = study.Transactions.Count(x => !x.IsWithinArmLength && x.AlpAdjustmentAmount > 0);
        study.TotalAlpAdjustmentAmount = study.Transactions.Sum(x => x.AlpAdjustmentAmount);

        study.TransactionsWithinToleranceCount = study.Transactions.Count(x => x.IsWithinArmLength);
        study.TransactionsOutsideToleranceCount = study.Transactions.Count(x => !x.IsWithinArmLength);
    }

    /// <summary>
    /// Generates full statutory text of Form No. 3CEB pursuant to Section 92E and Rule 10E.
    /// </summary>
    public static string GenerateForm3CEB(
        TransferPricingStudy study,
        string assesseeName,
        string pan,
        string registeredAddress)
    {
        var sb = new StringBuilder();
        var fy = study.FinancialYear;
        bool act2025 = IncomeTaxLaw.IsAct2025(fy); // Form No. 48 under section 172 from tax year 2026-27
        string S(string ref1961) => IncomeTaxLaw.Sec(ref1961, fy);
        string yearEnd = fy.Split('-').LastOrDefault() is { Length: 2 } yy ? "20" + yy : fy.Split('-').LastOrDefault() ?? "";

        sb.AppendLine("================================================================================");
        sb.AppendLine($"                               {IncomeTaxLaw.Form("3CEB", fy).ToUpperInvariant()}");
        sb.AppendLine(act2025
            ? "                     [See the Income-tax Rules, 2026]"
            : "                 [See rule 10E of Income-tax Rules, 1962]");
        sb.AppendLine($"        REPORT FROM AN ACCOUNTANT TO BE FURNISHED UNDER {S("92E").ToUpperInvariant()}");
        sb.AppendLine("                RELATING TO INTERNATIONAL TRANSACTION(S) AND");
        sb.AppendLine("                   SPECIFIED DOMESTIC TRANSACTION(S)");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        sb.AppendLine("1. I/We have examined the accounts and records of:");
        sb.AppendLine($"   Name of Assessee   : {assesseeName}");
        sb.AppendLine($"   PAN                : {pan}");
        sb.AppendLine($"   Registered Address : {registeredAddress}");
        if (act2025)
            sb.AppendLine($"   Tax Year           : {fy} (Ended 31st March, {yearEnd})");
        else
        {
            sb.AppendLine($"   Financial Year     : {fy} (Ended 31st March, {yearEnd})");
            sb.AppendLine($"   Assessment Year    : {study.AssessmentYear}");
        }
        sb.AppendLine();
        sb.AppendLine("2. In my/our opinion proper books of account and documentation as required by the");
        sb.AppendLine(act2025
            ? $"   provisions of sections 162 to 172 of the {IncomeTaxLaw.Act2025}, read with the Income-tax Rules, 2026,"
            : "   provisions of Chapter X of the Income-tax Act, 1961, read with rules 10D and 10E");
        sb.AppendLine($"   have been kept by the assessee in respect of international transactions and specified");
        sb.AppendLine($"   domestic transactions entered into by the assessee during the {(act2025 ? "tax year" : "previous year")}.");
        sb.AppendLine();
        sb.AppendLine($"3. The particulars required to be furnished under {S("92E")} are given in the");
        sb.AppendLine("   Annexure to this report. In my/our opinion and to the best of my/our information");
        sb.AppendLine("   and according to the explanations given to me/us, the particulars given in the");
        sb.AppendLine("   Annexure are true and correct.");
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("PART A - GENERAL INFORMATION");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"  1. Name of the Assessee              : {assesseeName}");
        sb.AppendLine($"  2. Address                           : {registeredAddress}");
        sb.AppendLine($"  3. Permanent Account Number (PAN)    : {pan}");
        sb.AppendLine($"  4. Status                            : Company / Body Corporate");
        sb.AppendLine($"  5. Residential Status                : Resident in India");
        sb.AppendLine(act2025
            ? $"  6. Tax Year ended                    : 31st March {yearEnd}"
            : $"  6. Previous Year ended               : 31st March {yearEnd}");
        sb.AppendLine(act2025
            ? $"  7. Tax Year                          : {fy}"
            : $"  7. Assessment Year                   : {study.AssessmentYear}");
        sb.AppendLine($"  8. Total Associated Enterprises      : {study.TotalAssociatedEnterprisesCount}");
        sb.AppendLine($"  9. Value of International Txs (INR)  : ₹{study.TotalInternationalInrValue:N2}");
        sb.AppendLine($" 10. Value of Specified Domestic Txs   : ₹{study.TotalSdtInrValue:N2}");
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("PART B - PARTICULARS OF INTERNATIONAL TRANSACTIONS (CLAUSES 11 TO 20)");
        sb.AppendLine("--------------------------------------------------------------------------------");

        var intlTxs = study.Transactions
            .Where(x => x.TransactionCategory == TransferPricingConstants.CatInternational || x.TransactionCategory == TransferPricingConstants.CatDeemedInternational)
            .ToList();

        if (intlTxs.Count == 0)
        {
            sb.AppendLine($"  No international transactions were entered into during the {(act2025 ? "tax year" : "previous year")}.");
        }
        else
        {
            int idx = 1;
            foreach (var tx in intlTxs)
            {
                var aeName = tx.AssociatedEnterprise?.EnterpriseName ?? "Associated Enterprise";
                var aeCountry = tx.AssociatedEnterprise?.CountryOfResidence ?? "Overseas";
                var aeTin = tx.AssociatedEnterprise?.TaxIdentificationNumber ?? "N/A";

                sb.AppendLine($"  Item #{idx++} [Clause {tx.ClauseNumber}]: {tx.DescriptionOfTransaction}");
                sb.AppendLine($"    Associated Enterprise   : {aeName} ({aeCountry}) | TIN: {aeTin}");
                sb.AppendLine($"    Transaction Value       : {tx.CurrencyCode} {tx.ForeignCurrencyAmount:N2} (INR Equivalent: ₹{tx.InrValue:N2})");
                sb.AppendLine($"    Exchange Rate           : 1 {tx.CurrencyCode} = ₹{tx.ExchangeRate:N4}");
                sb.AppendLine($"    Most Appropriate Method : {tx.MostAppropriateMethod} (PLI: {tx.ProfitLevelIndicator})");
                sb.AppendLine($"    Tested Party Margin     : {tx.TestedPartyMargin:N2}%");
                sb.AppendLine($"    Arm's Length Range/Mean : {tx.BenchmarkRangeMin:N2}% to {tx.BenchmarkRangeMax:N2}% (Median/Mean: {tx.BenchmarkMedianOrMean:N2}%)");
                sb.AppendLine($"    Arm's Length Status     : {(tx.IsWithinArmLength ? "AT ARM'S LENGTH" : "OUTSIDE ARM'S LENGTH RANGE")}");
                if (!tx.IsWithinArmLength && tx.AlpAdjustmentAmount > 0)
                {
                    sb.AppendLine($"    >> Primary TP Adjustment: ₹{tx.AlpAdjustmentAmount:N2} (Under {S("92CA")})");
                }
                sb.AppendLine($"    Documentation Reference : {tx.DocumentationReference}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("PART C - SPECIFIED DOMESTIC TRANSACTIONS (CLAUSES 21 TO 25)");
        sb.AppendLine("--------------------------------------------------------------------------------");

        var sdtTxs = study.Transactions
            .Where(x => x.TransactionCategory == TransferPricingConstants.CatSpecifiedDomestic)
            .ToList();

        if (sdtTxs.Count == 0)
        {
            sb.AppendLine($"  No specified domestic transactions exceeding the {S("92BA")} threshold (₹20 Cr) entered into.");
        }
        else
        {
            int idx = 1;
            foreach (var tx in sdtTxs)
            {
                var aeName = tx.AssociatedEnterprise?.EnterpriseName ?? "Related Domestic Enterprise";
                sb.AppendLine($"  SDT Item #{idx++} [Clause {tx.ClauseNumber}]: {tx.DescriptionOfTransaction}");
                sb.AppendLine($"    Related Enterprise      : {aeName}");
                sb.AppendLine($"    Transaction Value       : ₹{tx.InrValue:N2}");
                sb.AppendLine($"    Most Appropriate Method : {tx.MostAppropriateMethod} (PLI: {tx.ProfitLevelIndicator})");
                sb.AppendLine($"    Arm's Length Status     : {(tx.IsWithinArmLength ? "AT ARM'S LENGTH" : "OUTSIDE ARM'S LENGTH")}");
                if (!tx.IsWithinArmLength && tx.AlpAdjustmentAmount > 0)
                {
                    sb.AppendLine($"    >> Primary TP Adjustment: ₹{tx.AlpAdjustmentAmount:N2}");
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("SUMMARY OF PRIMARY TRANSFER PRICING ADJUSTMENTS (SECTION 92CA):");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"  Total Transactions Audited          : {study.Transactions.Count}");
        sb.AppendLine($"  Transactions At Arm's Length        : {study.TransactionsWithinToleranceCount}");
        sb.AppendLine($"  Transactions Outside Arm's Length   : {study.TransactionsOutsideToleranceCount}");
        sb.AppendLine($"  TOTAL PRIMARY TP ADJUSTMENTS (₹)    : ₹{study.TotalAlpAdjustmentAmount:N2}");
        sb.AppendLine();
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine("ACCOUNTANT'S CERTIFICATION & SIGN-OFF:");
        sb.AppendLine("--------------------------------------------------------------------------------");
        sb.AppendLine($"For and on behalf of:");
        sb.AppendLine($"Firm Name          : {study.AccountantFirmName}");
        sb.AppendLine($"Chartered Accountant: {study.AccountantName}");
        sb.AppendLine($"Membership Number  : {study.MembershipNumber}");
        sb.AppendLine($"FRN                : {study.FirmRegistrationNumber}");
        sb.AppendLine($"UDIN               : {study.Udin ?? "UNASSIGNED - PENDING CERTIFICATION"}");
        sb.AppendLine($"Date of Report     : {(study.CertifiedAt.HasValue ? study.CertifiedAt.Value.ToString("dd-MM-yyyy") : DateTime.UtcNow.ToString("dd-MM-yyyy"))}");
        sb.AppendLine($"Place              : New Delhi");
        sb.AppendLine("================================================================================");
        if (act2025)
        {
            sb.AppendLine();
            sb.AppendLine($"Note: clause numbers above follow the Form No. 3CEB layout; map them to {IncomeTaxLaw.Form("3CEB", fy)} before filing.");
        }

        return sb.ToString();
    }
}
