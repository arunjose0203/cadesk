using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CaDesk.Domain.Audit;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.Schedule3;

public static class Schedule3Division
{
    public const string DivisionI_AS = "Division I - AS (Non-Ind AS)";
    public const string DivisionII_IndAS = "Division II - Ind AS";
    public const string DivisionIII_NBFC = "Division III - NBFC (Ind AS)";

    public static readonly string[] All = [DivisionI_AS, DivisionII_IndAS, DivisionIII_NBFC];
}

public static class Schedule3RoundingUnit
{
    public const string Hundreds = "Hundreds";
    public const string Thousands = "Thousands";
    public const string Lakhs = "Lakhs";
    public const string Millions = "Millions";
    public const string Crores = "Crores";

    public static readonly string[] All = [Hundreds, Thousands, Lakhs, Millions, Crores];
}

public static class Schedule3Status
{
    public const string Draft = "draft";
    public const string InReview = "in_review";
    public const string Completed = "completed";
    public const string Finalized = "finalized";

    public static readonly string[] All = [Draft, InReview, Completed, Finalized];
}

public static class Schedule3RatioNames
{
    public const string CurrentRatio = "Current Ratio";
    public const string DebtEquityRatio = "Debt-Equity Ratio";
    public const string DebtServiceCoverageRatio = "Debt Service Coverage Ratio (DSCR)";
    public const string ReturnOnEquityRatio = "Return on Equity (ROE)";
    public const string InventoryTurnoverRatio = "Inventory Turnover Ratio";
    public const string TradeReceivablesTurnoverRatio = "Trade Receivables Turnover Ratio";
    public const string TradePayablesTurnoverRatio = "Trade Payables Turnover Ratio";
    public const string NetCapitalTurnoverRatio = "Net Capital Turnover Ratio";
    public const string NetProfitRatio = "Net Profit Ratio";
    public const string ReturnOnCapitalEmployed = "Return on Capital Employed (ROCE)";
    public const string ReturnOnInvestment = "Return on Investment (ROI)";
}

public class Schedule3Assessment : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Guid? AuditEngagementId { get; set; }
    public string FinancialYear { get; set; } = ""; // e.g. "2024-25"
    public string PrecedingFinancialYear { get; set; } = ""; // e.g. "2023-24"
    public string AccountingStandard { get; set; } = Schedule3Division.DivisionI_AS;
    public string RoundingUnit { get; set; } = Schedule3RoundingUnit.Lakhs;

    // Financial statement key metric inputs (Current Year and Preceding Year)
    public decimal TotalIncomeCurrent { get; set; }
    public decimal TotalIncomePreceding { get; set; }

    public decimal CurrentAssetsCurrent { get; set; }
    public decimal CurrentAssetsPreceding { get; set; }

    public decimal CurrentLiabilitiesCurrent { get; set; }
    public decimal CurrentLiabilitiesPreceding { get; set; }

    public decimal TotalDebtCurrent { get; set; }
    public decimal TotalDebtPreceding { get; set; }

    public decimal ShareholdersEquityCurrent { get; set; }
    public decimal ShareholdersEquityPreceding { get; set; }

    public decimal NetProfitAfterTaxesCurrent { get; set; }
    public decimal NetProfitAfterTaxesPreceding { get; set; }

    public decimal EbitCurrent { get; set; }
    public decimal EbitPreceding { get; set; }

    public decimal DebtServiceCurrent { get; set; }
    public decimal DebtServicePreceding { get; set; }

    public decimal CostOfGoodsSoldCurrent { get; set; }
    public decimal CostOfGoodsSoldPreceding { get; set; }

    public decimal AverageInventoryCurrent { get; set; }
    public decimal AverageInventoryPreceding { get; set; }

    public decimal RevenueFromOperationsCurrent { get; set; }
    public decimal RevenueFromOperationsPreceding { get; set; }

    public decimal AverageTradeReceivablesCurrent { get; set; }
    public decimal AverageTradeReceivablesPreceding { get; set; }

    public decimal NetCreditPurchasesCurrent { get; set; }
    public decimal NetCreditPurchasesPreceding { get; set; }

    public decimal AverageTradePayablesCurrent { get; set; }
    public decimal AverageTradePayablesPreceding { get; set; }

    public decimal CapitalEmployedCurrent { get; set; }
    public decimal CapitalEmployedPreceding { get; set; }

    public decimal IncomeFromInvestmentsCurrent { get; set; }
    public decimal IncomeFromInvestmentsPreceding { get; set; }

    public decimal AverageInvestmentsCurrent { get; set; }
    public decimal AverageInvestmentsPreceding { get; set; }

    // Rounding rule analysis
    public bool IsRoundingRuleCompliant { get; set; } = true;
    public string? RoundingRuleRecommendation { get; set; }

    // Summary counts
    public int SignificantVariancesCount { get; set; }
    public int MissingExplanationsCount { get; set; }

    // Status and Sign-Off
    public string AssessmentStatus { get; set; } = Schedule3Status.Draft;
    public string? AuditorRemarks { get; set; }
    public string? GeneratedDisclosureNoteText { get; set; }

    public Guid? FinalizedByUserId { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public AuditEngagement? AuditEngagement { get; set; }
    public User? FinalizedByUser { get; set; }

    public List<Schedule3RatioItem> Ratios { get; set; } = new();
    public List<Schedule3AgeingItem> AgeingItems { get; set; } = new();
}

public class Schedule3RatioItem : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid AssessmentId { get; set; }
    public string RatioCode { get; set; } = ""; // e.g., "R01", "R02", ... "R11"
    public string RatioName { get; set; } = "";
    public string NumeratorDescription { get; set; } = "";
    public string DenominatorDescription { get; set; } = "";

    public decimal CurrentYearValue { get; set; }
    public decimal PrecedingYearValue { get; set; }
    public decimal VariancePercent { get; set; }
    public bool IsVarianceSignificant { get; set; } // |Variance| >= 25%
    public string? VarianceExplanation { get; set; }
    public bool IsExplanationProvided => !string.IsNullOrWhiteSpace(VarianceExplanation);

    public Schedule3Assessment? Assessment { get; set; }
}

public class Schedule3AgeingItem : ITenantOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FirmId { get; set; }
    public Guid AssessmentId { get; set; }
    public string ScheduleType { get; set; } = "TradeReceivables"; // TradeReceivables, TradePayables, CWIP, Intangibles
    public string ParticularsCategory { get; set; } = ""; // e.g. "Undisputed Trade Receivables - considered good"

    public decimal Less6MonthsOr1Year { get; set; }
    public decimal OneToTwoYears { get; set; }
    public decimal TwoToThreeYears { get; set; }
    public decimal MoreThanThreeYears { get; set; }
    public decimal UnbilledDues { get; set; }
    public decimal Total => Less6MonthsOr1Year + OneToTwoYears + TwoToThreeYears + MoreThanThreeYears + UnbilledDues;

    public Schedule3Assessment? Assessment { get; set; }
}

public record Schedule3RoundingEvaluation(
    bool IsCompliant,
    string ChosenUnit,
    string AllowedUnitsDescription,
    string Rationale
);

public static class Schedule3Engine
{
    private static readonly CultureInfo IndianCulture = CultureInfo.GetCultureInfo("en-IN");
    private const decimal HundredCrores = 100_00_00_000m;

    public static Schedule3RoundingEvaluation ValidateRoundingRule(decimal totalIncome, string chosenUnit)
    {
        // MCA Schedule III General Instructions (Paragraph 4):
        // If Total Income < ₹100 Crores: Round off to nearest hundreds, thousands, lakhs, or millions, or decimals thereof.
        // If Total Income >= ₹100 Crores: Round off to nearest lakhs, millions, or crores, or decimals thereof.
        if (totalIncome >= HundredCrores)
        {
            var allowed = new[] { Schedule3RoundingUnit.Lakhs, Schedule3RoundingUnit.Millions, Schedule3RoundingUnit.Crores };
            bool ok = allowed.Contains(chosenUnit);
            return new Schedule3RoundingEvaluation(
                IsCompliant: ok,
                ChosenUnit: chosenUnit,
                AllowedUnitsDescription: "Lakhs, Millions, or Crores (Decimals permitted)",
                Rationale: ok
                    ? $"Compliant: Total income ({totalIncome.ToString("C2", IndianCulture)}) is >= ₹100 Crores; rounding off in {chosenUnit} complies with Schedule III."
                    : $"Non-compliant: Total income ({totalIncome.ToString("C2", IndianCulture)}) is >= ₹100 Crores; Schedule III mandates rounding off in Lakhs, Millions, or Crores (Hundreds or Thousands are not permitted)."
            );
        }
        else
        {
            var allowed = new[] { Schedule3RoundingUnit.Hundreds, Schedule3RoundingUnit.Thousands, Schedule3RoundingUnit.Lakhs, Schedule3RoundingUnit.Millions };
            bool ok = allowed.Contains(chosenUnit);
            return new Schedule3RoundingEvaluation(
                IsCompliant: ok,
                ChosenUnit: chosenUnit,
                AllowedUnitsDescription: "Hundreds, Thousands, Lakhs, or Millions (Decimals permitted)",
                Rationale: ok
                    ? $"Compliant: Total income ({totalIncome.ToString("C2", IndianCulture)}) is < ₹100 Crores; rounding off in {chosenUnit} complies with Schedule III."
                    : $"Notice: Total income is < ₹100 Crores; rounding unit {chosenUnit} should be verified against Schedule III guidelines."
            );
        }
    }

    public static List<Schedule3RatioItem> Compute11Ratios(Schedule3Assessment a)
    {
        var items = new List<Schedule3RatioItem>();

        // 1. Current Ratio = Current Assets / Current Liabilities
        items.Add(BuildRatio(
            "R01",
            Schedule3RatioNames.CurrentRatio,
            "Current Assets",
            "Current Liabilities",
            SafeDiv(a.CurrentAssetsCurrent, a.CurrentLiabilitiesCurrent),
            SafeDiv(a.CurrentAssetsPreceding, a.CurrentLiabilitiesPreceding)
        ));

        // 2. Debt-Equity Ratio = Total Debt / Shareholder's Equity
        items.Add(BuildRatio(
            "R02",
            Schedule3RatioNames.DebtEquityRatio,
            "Total Debt",
            "Shareholder's Equity",
            SafeDiv(a.TotalDebtCurrent, a.ShareholdersEquityCurrent),
            SafeDiv(a.TotalDebtPreceding, a.ShareholdersEquityPreceding)
        ));

        // 3. Debt Service Coverage Ratio (DSCR) = Earnings available for debt service / Debt Service
        items.Add(BuildRatio(
            "R03",
            Schedule3RatioNames.DebtServiceCoverageRatio,
            "Earnings available for debt service (EBIT)",
            "Debt Service (Interest & Principal Repayments)",
            SafeDiv(a.EbitCurrent, a.DebtServiceCurrent),
            SafeDiv(a.EbitPreceding, a.DebtServicePreceding)
        ));

        // 4. Return on Equity (ROE) % = (Net Profit after taxes / Shareholder's Equity) * 100
        items.Add(BuildRatio(
            "R04",
            Schedule3RatioNames.ReturnOnEquityRatio,
            "Net Profit after Taxes",
            "Average / Shareholder's Equity",
            SafeDiv(a.NetProfitAfterTaxesCurrent, a.ShareholdersEquityCurrent) * 100m,
            SafeDiv(a.NetProfitAfterTaxesPreceding, a.ShareholdersEquityPreceding) * 100m,
            isPercentage: true
        ));

        // 5. Inventory Turnover Ratio = Cost of Goods Sold / Average Inventory
        items.Add(BuildRatio(
            "R05",
            Schedule3RatioNames.InventoryTurnoverRatio,
            "Cost of Goods Sold / Materials Consumed",
            "Average Inventory",
            SafeDiv(a.CostOfGoodsSoldCurrent, a.AverageInventoryCurrent),
            SafeDiv(a.CostOfGoodsSoldPreceding, a.AverageInventoryPreceding)
        ));

        // 6. Trade Receivables Turnover Ratio = Revenue from Operations / Average Trade Receivables
        items.Add(BuildRatio(
            "R06",
            Schedule3RatioNames.TradeReceivablesTurnoverRatio,
            "Revenue from Operations (Net Credit Sales)",
            "Average Trade Receivables",
            SafeDiv(a.RevenueFromOperationsCurrent, a.AverageTradeReceivablesCurrent),
            SafeDiv(a.RevenueFromOperationsPreceding, a.AverageTradeReceivablesPreceding)
        ));

        // 7. Trade Payables Turnover Ratio = Net Credit Purchases / Average Trade Payables
        items.Add(BuildRatio(
            "R07",
            Schedule3RatioNames.TradePayablesTurnoverRatio,
            "Net Credit Purchases",
            "Average Trade Payables",
            SafeDiv(a.NetCreditPurchasesCurrent, a.AverageTradePayablesCurrent),
            SafeDiv(a.NetCreditPurchasesPreceding, a.AverageTradePayablesPreceding)
        ));

        // 8. Net Capital Turnover Ratio = Revenue from Operations / Working Capital
        decimal wcCurrent = a.CurrentAssetsCurrent - a.CurrentLiabilitiesCurrent;
        decimal wcPreceding = a.CurrentAssetsPreceding - a.CurrentLiabilitiesPreceding;
        items.Add(BuildRatio(
            "R08",
            Schedule3RatioNames.NetCapitalTurnoverRatio,
            "Revenue from Operations",
            "Working Capital (Current Assets - Current Liabilities)",
            SafeDiv(a.RevenueFromOperationsCurrent, wcCurrent),
            SafeDiv(a.RevenueFromOperationsPreceding, wcPreceding)
        ));

        // 9. Net Profit Ratio % = (Net Profit after taxes / Revenue from operations) * 100
        items.Add(BuildRatio(
            "R09",
            Schedule3RatioNames.NetProfitRatio,
            "Net Profit after Taxes",
            "Revenue from Operations",
            SafeDiv(a.NetProfitAfterTaxesCurrent, a.RevenueFromOperationsCurrent) * 100m,
            SafeDiv(a.NetProfitAfterTaxesPreceding, a.RevenueFromOperationsPreceding) * 100m,
            isPercentage: true
        ));

        // 10. Return on Capital Employed (ROCE) % = (EBIT / Capital Employed) * 100
        items.Add(BuildRatio(
            "R10",
            Schedule3RatioNames.ReturnOnCapitalEmployed,
            "Earnings Before Interest and Taxes (EBIT)",
            "Capital Employed",
            SafeDiv(a.EbitCurrent, a.CapitalEmployedCurrent) * 100m,
            SafeDiv(a.EbitPreceding, a.CapitalEmployedPreceding) * 100m,
            isPercentage: true
        ));

        // 11. Return on Investment (ROI) % = (Income from Investments / Average Investments) * 100
        items.Add(BuildRatio(
            "R11",
            Schedule3RatioNames.ReturnOnInvestment,
            "Income from Investments",
            "Average Investments",
            SafeDiv(a.IncomeFromInvestmentsCurrent, a.AverageInvestmentsCurrent) * 100m,
            SafeDiv(a.IncomeFromInvestmentsPreceding, a.AverageInvestmentsPreceding) * 100m,
            isPercentage: true
        ));

        return items;
    }

    private static Schedule3RatioItem BuildRatio(
        string code,
        string name,
        string numDesc,
        string denDesc,
        decimal current,
        decimal preceding,
        bool isPercentage = false)
    {
        current = Math.Round(current, 2);
        preceding = Math.Round(preceding, 2);

        decimal variancePct = 0m;
        if (preceding != 0m)
        {
            variancePct = Math.Round(((current - preceding) / Math.Abs(preceding)) * 100m, 2);
        }
        else if (current != 0m)
        {
            variancePct = 100m; // From 0 to positive value
        }

        bool isSignificant = Math.Abs(variancePct) >= 25.0m;

        return new Schedule3RatioItem
        {
            RatioCode = code,
            RatioName = name,
            NumeratorDescription = numDesc,
            DenominatorDescription = denDesc,
            CurrentYearValue = current,
            PrecedingYearValue = preceding,
            VariancePercent = variancePct,
            IsVarianceSignificant = isSignificant
        };
    }

    private static decimal SafeDiv(decimal num, decimal den)
    {
        if (den == 0m) return 0m;
        return num / den;
    }

    public static List<Schedule3AgeingItem> GetDefaultAgeingTemplates()
    {
        var list = new List<Schedule3AgeingItem>();

        // Trade Receivables
        list.Add(new Schedule3AgeingItem { ScheduleType = "TradeReceivables", ParticularsCategory = "(i) Undisputed Trade Receivables - considered good" });
        list.Add(new Schedule3AgeingItem { ScheduleType = "TradeReceivables", ParticularsCategory = "(ii) Undisputed Trade Receivables - which have significant increase in credit risk" });
        list.Add(new Schedule3AgeingItem { ScheduleType = "TradeReceivables", ParticularsCategory = "(iii) Undisputed Trade Receivables - credit impaired" });
        list.Add(new Schedule3AgeingItem { ScheduleType = "TradeReceivables", ParticularsCategory = "(iv) Disputed Trade Receivables - considered good" });
        list.Add(new Schedule3AgeingItem { ScheduleType = "TradeReceivables", ParticularsCategory = "(v) Disputed Trade Receivables - which have significant increase in credit risk" });
        list.Add(new Schedule3AgeingItem { ScheduleType = "TradeReceivables", ParticularsCategory = "(vi) Disputed Trade Receivables - credit impaired" });

        // Trade Payables
        list.Add(new Schedule3AgeingItem { ScheduleType = "TradePayables", ParticularsCategory = "(i) MSME dues" });
        list.Add(new Schedule3AgeingItem { ScheduleType = "TradePayables", ParticularsCategory = "(ii) Others" });
        list.Add(new Schedule3AgeingItem { ScheduleType = "TradePayables", ParticularsCategory = "(iii) Disputed dues - MSME" });
        list.Add(new Schedule3AgeingItem { ScheduleType = "TradePayables", ParticularsCategory = "(iv) Disputed dues - Others" });

        // Capital Work-in-Progress (CWIP)
        list.Add(new Schedule3AgeingItem { ScheduleType = "CWIP", ParticularsCategory = "(i) Projects in progress" });
        list.Add(new Schedule3AgeingItem { ScheduleType = "CWIP", ParticularsCategory = "(ii) Projects temporarily suspended" });

        return list;
    }

    public static string GenerateDisclosureNoteText(
        Schedule3Assessment a,
        IEnumerable<Schedule3RatioItem> ratios,
        IEnumerable<Schedule3AgeingItem> ageingItems)
    {
        var sb = new StringBuilder();

        sb.AppendLine("NOTE: ADDITIONAL REGULATORY INFORMATION & FINANCIAL RATIOS");
        sb.AppendLine("(Pursuant to Schedule III to the Companies Act, 2013)");
        sb.AppendLine(new string('=', 78));
        sb.AppendLine();

        sb.AppendLine($"Company Name     : {a.Client?.Name ?? "Company"}");
        sb.AppendLine($"Financial Year   : {a.FinancialYear} (Preceding Year: {a.PrecedingFinancialYear})");
        sb.AppendLine($"Standard / Div   : {a.AccountingStandard}");
        sb.AppendLine($"Unit of Measure  : In {a.RoundingUnit}");
        sb.AppendLine();

        sb.AppendLine("1. ANALYTICAL FINANCIAL RATIOS SCHEDULE:");
        sb.AppendLine(new string('-', 78));
        sb.AppendLine(string.Format("{0,-35} | {1,10} | {2,10} | {3,10} | {4,-6}",
            "Ratio Name", a.FinancialYear, a.PrecedingFinancialYear, "Variance %", "Flag"));
        sb.AppendLine(new string('-', 78));

        var ratioList = ratios.OrderBy(r => r.RatioCode).ToList();
        foreach (var r in ratioList)
        {
            string flag = r.IsVarianceSignificant ? ">= 25%" : "Normal";
            sb.AppendLine(string.Format("{0,-35} | {1,10:F2} | {2,10:F2} | {3,9:F2}% | {4,-6}",
                r.RatioName, r.CurrentYearValue, r.PrecedingYearValue, r.VariancePercent, flag));
        }
        sb.AppendLine(new string('-', 78));
        sb.AppendLine();

        // Explanations for significant variances
        var significant = ratioList.Where(r => r.IsVarianceSignificant).ToList();
        sb.AppendLine("2. EXPLANATION OF SIGNIFICANT RATIO VARIANCES (Change >= 25%):");
        sb.AppendLine(new string('-', 78));
        if (significant.Count == 0)
        {
            sb.AppendLine("   * None of the financial ratios had a variance of 25% or more compared to the preceding financial year.");
        }
        else
        {
            int idx = 1;
            foreach (var s in significant)
            {
                sb.AppendLine($"   ({idx++}) {s.RatioName}:");
                sb.AppendLine($"       Variance: {s.VariancePercent:F2}% ({a.FinancialYear}: {s.CurrentYearValue:F2} vs {a.PrecedingFinancialYear}: {s.PrecedingYearValue:F2})");
                sb.AppendLine($"       Formula : Numerator [{s.NumeratorDescription}] / Denominator [{s.DenominatorDescription}]");
                string exp = !string.IsNullOrWhiteSpace(s.VarianceExplanation)
                    ? s.VarianceExplanation
                    : "[PENDING AUDITOR EXPLANATION: Mandatory Schedule III disclosure required]";
                sb.AppendLine($"       Reason  : {exp}");
                sb.AppendLine();
            }
        }

        // Ageing Schedules
        var trAgeing = ageingItems.Where(x => x.ScheduleType == "TradeReceivables").ToList();
        if (trAgeing.Count > 0)
        {
            sb.AppendLine("3. TRADE RECEIVABLES AGEING SCHEDULE:");
            sb.AppendLine(new string('-', 78));
            sb.AppendLine(string.Format("{0,-32} | {1,7} | {2,7} | {3,7} | {4,7} | {5,7} | {6,8}",
                "Particulars", "< 6m", "6m-1y", "1-2y", "2-3y", "> 3y", "Total"));
            sb.AppendLine(new string('-', 78));
            foreach (var item in trAgeing)
            {
                sb.AppendLine(string.Format("{0,-32} | {1,7:F0} | {2,7:F0} | {3,7:F0} | {4,7:F0} | {5,7:F0} | {6,8:F0}",
                    item.ParticularsCategory.Length > 32 ? item.ParticularsCategory.Substring(0, 32) : item.ParticularsCategory,
                    item.Less6MonthsOr1Year, item.OneToTwoYears, item.TwoToThreeYears, item.MoreThanThreeYears, item.UnbilledDues, item.Total));
            }
            sb.AppendLine(new string('-', 78));
            sb.AppendLine();
        }

        sb.AppendLine("4. GENERAL INSTRUCTIONS ON ROUNDING OFF COMPLIANCE:");
        sb.AppendLine(new string('-', 78));
        sb.AppendLine($"   Total Income Reported: {a.TotalIncomeCurrent.ToString("C2", IndianCulture)}");
        sb.AppendLine($"   Unit Selected        : In {a.RoundingUnit}");
        sb.AppendLine($"   Compliance Status    : {(a.IsRoundingRuleCompliant ? "COMPLIANT WITH SCHEDULE III" : "NON-COMPLIANT")}");
        if (!string.IsNullOrWhiteSpace(a.RoundingRuleRecommendation))
        {
            sb.AppendLine($"   Finding & Rule       : {a.RoundingRuleRecommendation}");
        }
        sb.AppendLine(new string('-', 78));
        sb.AppendLine();
        sb.AppendLine("For and on behalf of the Board of Directors");
        sb.AppendLine("As per our report of even date attached.");
        sb.AppendLine($"Date: {DateTime.UtcNow:dd-MM-yyyy}");

        return sb.ToString();
    }
}
