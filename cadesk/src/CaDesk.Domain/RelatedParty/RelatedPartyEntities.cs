using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.RelatedParty;

public class RelatedPartyEntity : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string Name { get; set; } = string.Empty;
    public string RelationshipType { get; set; } = "HoldingCompany"; // HoldingCompany, Subsidiary, FellowSubsidiary, Associate, JointVenture, KeyManagementPersonnel, RelativeOfKMP, EnterpriseControlledByKMP
    public string Pan { get; set; } = string.Empty;
    public string Cin { get; set; } = string.Empty;
    public string DirectorOrKmpName { get; set; } = string.Empty;
    public string NatureOfInterest { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public List<RelatedPartyTransaction> Transactions { get; set; } = [];
}

public class RelatedPartyTransaction : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid RelatedPartyId { get; set; }
    public RelatedPartyEntity? RelatedParty { get; set; }

    public string FinancialYear { get; set; } = "2024-25";
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public string ContractReference { get; set; } = string.Empty;
    public string TransactionType { get; set; } = "SaleOfGoods"; // SaleOfGoods, PurchaseOfGoods, LeaseOfProperty, RenderingOfServices, AvailingOfServices, RemunerationOfficeOfProfit, LoanGiven, LoanReceived, GuaranteeUnderwriting, Other
    public string Description { get; set; } = string.Empty;
    public decimal TransactionAmount { get; set; }
    public string Currency { get; set; } = "INR";

    // Benchmarking & Threshold Evaluation (Section 188 / Rule 15)
    public decimal AnnualTurnoverBenchmark { get; set; }
    public decimal NetWorthBenchmark { get; set; }
    public decimal ThresholdLimitAmount { get; set; }
    public decimal ThresholdPercentage { get; set; } = 10.00m;
    public bool ExceedsStatutoryThreshold { get; set; }

    // Arm's length & Ordinary Course of Business (Section 188(1) 4th Proviso)
    public bool IsOrdinaryCourseOfBusiness { get; set; } = true;
    public bool IsArmLength { get; set; } = true;
    public string ArmLengthMethod { get; set; } = "CUP"; // CUP, ResalePriceMethod, CostPlusMethod, TNMM, IndependentValuation, NotApplicable
    public decimal BenchmarkArmLengthPrice { get; set; }
    public decimal PriceVariancePercentage { get; set; }

    // Approvals (Section 177 / 188)
    public string AuditCommitteeApprovalStatus { get; set; } = "Approved"; // Pending, Approved, OmnibusApproved, Exempt
    public DateTime? AuditCommitteeMeetingDate { get; set; }
    public Guid? OmnibusApprovalId { get; set; }
    public DateTime? BoardResolutionDate { get; set; }
    public DateTime? ShareholdersResolutionDate { get; set; }
    public bool RequiresShareholderApproval { get; set; }

    // Income tax & Disclosure particulars
    public bool Section40A2bRisk { get; set; } // Excessive/unreasonable under IT Act
    public DateTime? Mbp4EnteredDate { get; set; } // Date entered in Form MBP-4 Register
    public decimal OutstandingBalanceAtYearEnd { get; set; }
    public string BalanceType { get; set; } = "Debit"; // Debit, Credit, Nil
    public string Status { get; set; } = "Draft"; // Draft, Reviewed, Approved, Disclosed
}

public class RelatedPartyOmnibusApproval : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string FinancialYear { get; set; } = "2024-25";
    public DateTime AuditCommitteeMeetingDate { get; set; } = DateTime.UtcNow;
    public string Title { get; set; } = string.Empty;
    public decimal MaximumApprovedLimit { get; set; }
    public decimal UtilizedAmount { get; set; }
    public string ApprovalCriteria { get; set; } = string.Empty;
    public DateTime ValidityPeriodTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public static class RelatedPartyEngine
{
    public static void EvaluateTransaction(RelatedPartyTransaction tx, decimal companyTurnover, decimal companyNetWorth)
    {
        tx.AnnualTurnoverBenchmark = companyTurnover;
        tx.NetWorthBenchmark = companyNetWorth;

        // Rule 15 thresholds:
        // 1. Sale/Purchase of goods/materials: >= 10% of annual turnover
        // 2. Selling/Buying property: >= 10% of net worth
        // 3. Leasing of property: >= 10% of annual turnover
        // 4. Availing/Rendering services: >= 10% of annual turnover
        // 5. Office of profit remuneration: >= 2,50,000/month (30,00,000/yr)
        switch (tx.TransactionType)
        {
            case "SaleOfGoods":
            case "PurchaseOfGoods":
            case "LeaseOfProperty":
            case "RenderingOfServices":
            case "AvailingOfServices":
                tx.ThresholdPercentage = 10.00m;
                tx.ThresholdLimitAmount = Math.Round(companyTurnover * 0.10m, 2);
                tx.ExceedsStatutoryThreshold = tx.ThresholdLimitAmount > 0 && tx.TransactionAmount >= tx.ThresholdLimitAmount;
                break;

            case "SellingBuyingProperty":
            case "GuaranteeUnderwriting":
                tx.ThresholdPercentage = 10.00m;
                tx.ThresholdLimitAmount = Math.Round(companyNetWorth * 0.10m, 2);
                tx.ExceedsStatutoryThreshold = tx.ThresholdLimitAmount > 0 && tx.TransactionAmount >= tx.ThresholdLimitAmount;
                break;

            case "RemunerationOfficeOfProfit":
                tx.ThresholdPercentage = 0m;
                tx.ThresholdLimitAmount = 3000000m; // ₹2.5 Lakhs/month = ₹30 Lakhs/year
                tx.ExceedsStatutoryThreshold = tx.TransactionAmount > tx.ThresholdLimitAmount;
                break;

            default:
                tx.ThresholdPercentage = 10.00m;
                tx.ThresholdLimitAmount = Math.Round(companyTurnover * 0.10m, 2);
                tx.ExceedsStatutoryThreshold = tx.ThresholdLimitAmount > 0 && tx.TransactionAmount >= tx.ThresholdLimitAmount;
                break;
        }

        // Arm's Length Price evaluation
        if (tx.BenchmarkArmLengthPrice > 0)
        {
            decimal diff = tx.TransactionAmount - tx.BenchmarkArmLengthPrice;
            tx.PriceVariancePercentage = Math.Round((diff / tx.BenchmarkArmLengthPrice) * 100m, 2);

            // For sales/income: actual >= benchmark is good. For purchases/expenses: actual <= benchmark is good.
            // Tolerance within +/- 3% is considered arm's length under Income Tax Transfer Pricing rules.
            if (Math.Abs(tx.PriceVariancePercentage) <= 3.0m)
            {
                tx.IsArmLength = true;
            }
            else if (tx.TransactionType.StartsWith("Sale") || tx.TransactionType.StartsWith("Rendering"))
            {
                // Charging more or equal to market
                tx.IsArmLength = tx.TransactionAmount >= tx.BenchmarkArmLengthPrice;
            }
            else
            {
                // Paying less or equal to market
                tx.IsArmLength = tx.TransactionAmount <= tx.BenchmarkArmLengthPrice;
            }
        }

        // Section 40A(2)(b) excessive expenditure risk
        if (!tx.IsArmLength && (tx.TransactionType.StartsWith("Purchase") || tx.TransactionType.StartsWith("Availing") || tx.TransactionType == "RemunerationOfficeOfProfit"))
        {
            tx.Section40A2bRisk = true;
        }
        else
        {
            tx.Section40A2bRisk = false;
        }

        // Section 188(1) 4th proviso:
        // Transactions entered in ordinary course of business AND on arm's length basis are exempt from Section 188(1) Board/Shareholder approvals.
        if (tx.IsOrdinaryCourseOfBusiness && tx.IsArmLength)
        {
            tx.RequiresShareholderApproval = false;
        }
        else if (tx.ExceedsStatutoryThreshold)
        {
            // Not ordinary course OR not arm's length, and exceeds Rule 15 limit => Mandatory Special Resolution
            tx.RequiresShareholderApproval = true;
        }
        else
        {
            tx.RequiresShareholderApproval = false;
        }
    }

    public static string GenerateMbp4Register(List<RelatedPartyTransaction> transactions, string companyName, string financialYear)
    {
        var sb = new StringBuilder();
        sb.AppendLine("====================================================================================================");
        sb.AppendLine("                                            FORM MBP - 4                                            ");
        sb.AppendLine("                                  [Pursuant to Section 189(1) and rule 16(1)]                       ");
        sb.AppendLine("        Register of contracts or arrangements in which directors are interested under Section 184   ");
        sb.AppendLine("                     or contracts/arrangements with related parties under Section 188               ");
        sb.AppendLine("====================================================================================================");
        sb.AppendLine();
        sb.AppendLine($"Company Name     : {companyName}");
        sb.AppendLine($"Financial Year   : {financialYear}");
        sb.AppendLine($"Generated Date   : {DateTime.UtcNow:dd-MMM-yyyy}");
        sb.AppendLine();
        sb.AppendLine("PART B: Contracts or arrangements with related parties under Section 188");
        sb.AppendLine("----------------------------------------------------------------------------------------------------------------");
        // a statutory register must show full names, so the columns grow to fit instead of truncating
        var nameWidth = Math.Max(32, transactions.Select(x => (x.RelatedParty?.Name ?? "Unknown").Length).DefaultIfEmpty(0).Max());
        var relWidth = Math.Max(24, transactions.Select(x => (x.RelatedParty?.RelationshipType ?? "RelatedParty").Length).DefaultIfEmpty(0).Max());
        var rowFormat = "{0,-12} | {1,-" + nameWidth + "} | {2,-" + relWidth + "} | {3,-15} | {4,14:N2} | {5,-10} | {6,-10}";
        sb.AppendLine(string.Format(rowFormat.Replace(":N2", ""),
            "Date", "Related Party", "Relationship", "Type", "Amount (INR)", "Arm's Length", "Sec 188 Appr"));
        sb.AppendLine("----------------------------------------------------------------------------------------------------------------");

        foreach (var t in transactions.OrderBy(x => x.TransactionDate))
        {
            var partyName = t.RelatedParty?.Name ?? "Unknown";
            var relType = t.RelatedParty?.RelationshipType ?? "RelatedParty";

            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, rowFormat,
                t.TransactionDate.ToString("dd-MM-yyyy"),
                partyName,
                relType,
                t.TransactionType,
                t.TransactionAmount,
                t.IsArmLength ? "YES" : "NO",
                t.AuditCommitteeApprovalStatus));
        }

        sb.AppendLine("----------------------------------------------------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("STATUTORY DECLARATION & NOTES:");
        sb.AppendLine("1. This register has been maintained in accordance with the provisions of Section 189 of Companies Act, 2013.");
        sb.AppendLine("2. All transactions entered in ordinary course of business and on arm's length basis have been verified.");
        sb.AppendLine("3. Entries in this register were authenticated by the Company Secretary / Director at the subsequent Board meeting.");
        sb.AppendLine("====================================================================================================");

        return sb.ToString();
    }

    public static string GenerateAs18IndAs24DisclosureNote(List<RelatedPartyTransaction> transactions, string companyName, string financialYear)
    {
        var sb = new StringBuilder();
        sb.AppendLine("====================================================================================================");
        sb.AppendLine("                NOTES FORMING PART OF FINANCIAL STATEMENTS FOR THE YEAR ENDED MARCH 31              ");
        sb.AppendLine("                      NOTE: RELATED PARTY DISCLOSURES (AS 18 / Ind AS 24)                          ");
        sb.AppendLine("====================================================================================================");
        sb.AppendLine();
        sb.AppendLine($"Entity Name    : {companyName}");
        sb.AppendLine($"Financial Year : {financialYear}");
        sb.AppendLine();
        sb.AppendLine("(A) LIST OF RELATED PARTIES WHERE CONTROL / SIGNIFICANT INFLUENCE EXISTS:");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");

        var distinctParties = transactions
            .Where(x => x.RelatedParty != null)
            .Select(x => x.RelatedParty!)
            .DistinctBy(x => x.Id)
            .ToList();

        var groupedByType = distinctParties.GroupBy(x => x.RelationshipType).OrderBy(g => g.Key);

        int catIndex = 1;
        foreach (var grp in groupedByType)
        {
            sb.AppendLine($"{catIndex}. {grp.Key}:");
            foreach (var p in grp)
            {
                sb.AppendLine($"   • {p.Name} (PAN: {p.Pan}) - Nature of Interest: {p.NatureOfInterest}");
            }
            catIndex++;
        }

        sb.AppendLine();
        sb.AppendLine("(B) SUMMARY OF TRANSACTIONS DURING THE FINANCIAL YEAR (INR):");
        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine(string.Format("{0,-28} | {1,-20} | {2,16} | {3,16} | {4,-10}",
            "Nature of Transaction", "Relationship Category", "Current Year", "Year-End Balance", "Balance Type"));
        sb.AppendLine("----------------------------------------------------------------------------------------------------");

        var transGroups = transactions
            .GroupBy(x => new { x.TransactionType, Rel = x.RelatedParty?.RelationshipType ?? "Other" })
            .OrderBy(g => g.Key.TransactionType);

        foreach (var tg in transGroups)
        {
            decimal totalAmt = tg.Sum(x => x.TransactionAmount);
            decimal totalClosing = tg.Sum(x => x.OutstandingBalanceAtYearEnd);
            string balType = tg.Select(x => x.BalanceType).FirstOrDefault() ?? "Debit";

            sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0,-28} | {1,-20} | {2,16:N2} | {3,16:N2} | {4,-10}",
                tg.Key.TransactionType,
                tg.Key.Rel,
                totalAmt,
                totalClosing,
                balType));
        }

        sb.AppendLine("----------------------------------------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("(C) TERMS AND CONDITIONS OF TRANSACTIONS WITH RELATED PARTIES:");
        sb.AppendLine("All transactions with related parties were made on terms equivalent to those that prevail in arm's length");
        sb.AppendLine("transactions, in the ordinary course of business. Outstanding balances at the year-end are unsecured and");
        sb.AppendLine("interest-free (except loans where statutory interest under Section 186 has been stipulated) and settlement");
        sb.AppendLine("occurs in cash. No expense has been recognized in the current year or prior years in respect of bad or");
        sb.AppendLine("doubtful debts due from related parties.");
        sb.AppendLine("====================================================================================================");

        return sb.ToString();
    }
}
