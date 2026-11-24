using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.Ibc;

public enum CirpStage
{
    AdmissionAndMoratorium = 1,
    PublicAnnouncementAndClaims = 2,
    CocConstituted = 3,
    InformationMemorandumIssued = 4,
    FormGInvited = 5,
    ResolutionPlanReview = 6,
    ResolutionPlanApproved = 7,
    LiquidationInitiated = 8,
    Section12AWithdrawn = 9
}

public enum CreditorCategory
{
    SecuredFinancialCreditor = 1,
    UnsecuredFinancialCreditor = 2,
    OperationalCreditorEmployees = 3,
    OperationalCreditorGovernment = 4,
    OperationalCreditorVendors = 5
}

public enum PufeType
{
    PreferentialSec43 = 1,
    UndervaluedSec45 = 2,
    ExtortionateCreditSec50 = 3,
    FraudulentTradingSec66 = 4
}

public enum AssetValuationClass
{
    LandAndBuilding = 1,
    PlantAndMachinery = 2,
    SecuritiesAndFinancialAssets = 3
}

public enum VotingResolutionStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public class CirpCase : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public string CorporateDebtorName { get; set; } = "";
    public string Cin { get; set; } = "";
    public string NcltBench { get; set; } = "NCLT New Delhi Bench-III";
    public string CourtOrderNumber { get; set; } = "";
    public DateOnly AdmissionDate { get; set; } // Insolvency Commencement Date (ICD)

    public string IrpName { get; set; } = "";
    public string IrpIpRegNumber { get; set; } = "";
    public string RpName { get; set; } = "";
    public string RpIpRegNumber { get; set; } = "";

    public bool MoratoriumSection14Declared { get; set; } = true;
    public DateOnly ClaimsLastDate { get; set; } // ICD + 14 days
    public DateOnly Initial180DaysDeadline { get; set; } // ICD + 180 days
    public DateOnly Extended270DaysDeadline { get; set; } // ICD + 270 days
    public DateOnly Final330DaysLimit { get; set; } // ICD + 330 days

    public CirpStage Stage { get; set; } = CirpStage.AdmissionAndMoratorium;
    public decimal TotalAdmittedFinancialDebt { get; set; }
    public decimal TotalAdmittedOperationalDebt { get; set; }
    public string StatusNote { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<CocMember> CocMembers { get; set; } = new();
    public List<CocResolutionItem> Resolutions { get; set; } = new();
    public List<PufeTransactionItem> PufeTransactions { get; set; } = new();
    public List<AssetValuationItem> Valuations { get; set; } = new();
}

public class CocMember : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid CirpCaseId { get; set; }
    public CirpCase? CirpCase { get; set; }

    public string CreditorName { get; set; } = "";
    public CreditorCategory Category { get; set; } = CreditorCategory.SecuredFinancialCreditor;
    public decimal ClaimSubmittedAmount { get; set; }
    public decimal AdmittedClaimAmount { get; set; }
    public decimal VotingSharePct { get; set; } // Proportional to financial debt admitted
    public string SecurityInterestDescription { get; set; } = "";
    public bool IsRelatedParty { get; set; } // Disqualified from voting u/s 21(2) first proviso

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class CocResolutionItem : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid CirpCaseId { get; set; }
    public CirpCase? CirpCase { get; set; }

    public int MeetingNumber { get; set; } = 1;
    public string ResolutionNumber { get; set; } = "RES-01";
    public string ResolutionTitle { get; set; } = "";
    public string StatutorySection { get; set; } = "Section 30(4)"; // e.g. Sec 30(4) Resolution Plan (66%), Sec 12A Withdrawal (90%), Routine (51%)
    public decimal RequiredThresholdPct { get; set; } = 66m; // 51%, 66%, or 90%

    public decimal VotingShareInFavourPct { get; set; }
    public decimal VotingShareAgainstPct { get; set; }
    public decimal VotingShareAbstainedPct { get; set; }
    public VotingResolutionStatus Status { get; set; } = VotingResolutionStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class PufeTransactionItem : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid CirpCaseId { get; set; }
    public CirpCase? CirpCase { get; set; }

    public PufeType PufeType { get; set; } = PufeType.PreferentialSec43;
    public DateOnly TransactionDate { get; set; }
    public string CounterpartyName { get; set; } = "";
    public bool IsRelatedParty { get; set; }
    public decimal TransactionAmount { get; set; }
    public decimal EstimatedAvoidanceRecoverableAmount { get; set; }
    public int LookbackPeriodYears { get; set; } = 1; // 1 year for unrelated, 2 years for related
    public bool IsWithinLookbackPeriod { get; set; }
    public string ForensicAuditorObservations { get; set; } = "";
    public bool NcltApplicationFiled { get; set; } // Form filed under Regulation 35A

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class AssetValuationItem : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid CirpCaseId { get; set; }
    public CirpCase? CirpCase { get; set; }

    public AssetValuationClass AssetClass { get; set; } = AssetValuationClass.LandAndBuilding;
    public string AssetDescription { get; set; } = "";

    public string Valuer1Name { get; set; } = "";
    public decimal Valuer1FairValue { get; set; }
    public decimal Valuer1LiquidationValue { get; set; }

    public string Valuer2Name { get; set; } = "";
    public decimal Valuer2FairValue { get; set; }
    public decimal Valuer2LiquidationValue { get; set; }

    public decimal AverageFairValue { get; set; }
    public decimal AverageLiquidationValue { get; set; }
    public bool ThirdValuerRequired { get; set; } // Required if difference > 25% under Reg 35(1)(b)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public static class IbcCirpEngine
{
    public record CirpTimelineResult(
        DateOnly AdmissionDate,
        DateOnly ClaimsLastDate,
        DateOnly Initial180Days,
        DateOnly Extended270Days,
        DateOnly Final330Days,
        int DaysElapsed,
        int DaysRemainingTo180,
        int DaysRemainingTo330,
        bool Is180DaysExpired,
        bool Is330DaysCapBreached,
        string TimelineStatusAlert
    );

    /// <summary>
    /// Computes statutory CIRP deadlines under IBC Section 12 & IBBI CIRP Regulations
    /// </summary>
    public static CirpTimelineResult CalculateCirpTimelines(DateOnly admissionDate, DateOnly asOfDate)
    {
        var claimsLastDate = admissionDate.AddDays(14);
        var initial180 = admissionDate.AddDays(180);
        var extended270 = admissionDate.AddDays(270);
        var final330 = admissionDate.AddDays(330);

        int daysElapsed = asOfDate.DayNumber - admissionDate.DayNumber;
        int remainingTo180 = initial180.DayNumber - asOfDate.DayNumber;
        int remainingTo330 = final330.DayNumber - asOfDate.DayNumber;

        bool is180Expired = asOfDate > initial180;
        bool is330Breached = asOfDate > final330;

        string alert;
        if (is330Breached)
        {
            alert = "CRITICAL: 330-day absolute statutory cap under Section 12(3) 2nd proviso breached. Mandatory liquidation u/s 33 attracted unless NCLT exclusion granted.";
        }
        else if (is180Expired)
        {
            alert = $"Initial 180-day CIRP period expired. Extension up to 270 days required via CoC 66% vote u/s 12(2). {remainingTo330} days remaining to absolute 330-day limit.";
        }
        else if (remainingTo180 <= 30)
        {
            alert = $"WARNING: Only {remainingTo180} days remaining to initial 180-day CIRP deadline. Convene CoC for extension vote or submit Resolution Plan.";
        }
        else
        {
            alert = $"CIRP in progress. {remainingTo180} days remaining to initial 180-day deadline.";
        }

        return new CirpTimelineResult(
            AdmissionDate: admissionDate,
            ClaimsLastDate: claimsLastDate,
            Initial180Days: initial180,
            Extended270Days: extended270,
            Final330Days: final330,
            DaysElapsed: daysElapsed,
            DaysRemainingTo180: remainingTo180,
            DaysRemainingTo330: remainingTo330,
            Is180DaysExpired: is180Expired,
            Is330DaysCapBreached: is330Breached,
            TimelineStatusAlert: alert
        );
    }

    /// <summary>
    /// Computes CoC voting shares. Only Financial Creditors who are NOT related parties u/s 21(2) are eligible to vote.
    /// </summary>
    public static void CalculateCocVotingShares(List<CocMember> members)
    {
        // Eligible voters are Financial Creditors who are NOT related parties
        var eligible = members
            .Where(m => (m.Category == CreditorCategory.SecuredFinancialCreditor || m.Category == CreditorCategory.UnsecuredFinancialCreditor) && !m.IsRelatedParty)
            .ToList();

        decimal totalEligibleDebt = eligible.Sum(m => m.AdmittedClaimAmount);

        foreach (var m in members)
        {
            if (m.IsRelatedParty || (m.Category != CreditorCategory.SecuredFinancialCreditor && m.Category != CreditorCategory.UnsecuredFinancialCreditor))
            {
                m.VotingSharePct = 0m;
            }
            else if (totalEligibleDebt > 0)
            {
                m.VotingSharePct = Math.Round((m.AdmittedClaimAmount / totalEligibleDebt) * 100m, 2);
            }
            else
            {
                m.VotingSharePct = 0m;
            }
        }
    }

    /// <summary>
    /// Evaluates voting resolution against IBC statutory thresholds (51%, 66%, 90%)
    /// </summary>
    public static VotingResolutionStatus EvaluateResolution(decimal requiredThreshold, decimal votingInFavour)
    {
        return votingInFavour >= requiredThreshold ? VotingResolutionStatus.Approved : VotingResolutionStatus.Rejected;
    }

    /// <summary>
    /// Validates PUFE transaction look-back period under Sections 43, 45, 50, 66
    /// </summary>
    public static bool EvaluatePufeLookback(DateOnly admissionDate, DateOnly transactionDate, bool isRelatedParty, PufeType pufeType)
    {
        if (pufeType == PufeType.FraudulentTradingSec66)
        {
            // Section 66 has no look-back time limitation
            return true;
        }

        int lookbackYears = 1;
        if (pufeType == PufeType.ExtortionateCreditSec50)
        {
            lookbackYears = 2; // Always 2 years u/s 50
        }
        else if (isRelatedParty)
        {
            lookbackYears = 2; // 2 years for related parties u/s 43(4)(a) and u/s 46(1)
        }

        var lookbackStartDate = admissionDate.AddYears(-lookbackYears);
        return transactionDate >= lookbackStartDate && transactionDate <= admissionDate;
    }

    /// <summary>
    /// Computes Fair Value & Liquidation Value under Regulation 35 and detects >25% variation triggering 3rd valuer
    /// </summary>
    public static (decimal avgFair, decimal avgLiq, bool thirdValuerRequired) ComputeValuation(
        decimal v1Fair, decimal v1Liq, decimal v2Fair, decimal v2Liq)
    {
        decimal avgFair = Math.Round((v1Fair + v2Fair) / 2m, 2);
        decimal avgLiq = Math.Round((v1Liq + v2Liq) / 2m, 2);

        // Check if difference between two estimates is > 25% of higher value
        decimal maxFair = Math.Max(v1Fair, v2Fair);
        decimal minFair = Math.Min(v1Fair, v2Fair);
        bool fairDiffExceeded = maxFair > 0 && ((maxFair - minFair) / maxFair) > 0.25m;

        decimal maxLiq = Math.Max(v1Liq, v2Liq);
        decimal minLiq = Math.Min(v1Liq, v2Liq);
        bool liqDiffExceeded = maxLiq > 0 && ((maxLiq - minLiq) / maxLiq) > 0.25m;

        bool thirdValuer = fairDiffExceeded || liqDiffExceeded;
        return (avgFair, avgLiq, thirdValuer);
    }

    /// <summary>
    /// Generates formal, print-ready Information Memorandum conforming to IBBI Regulation 36
    /// </summary>
    public static string GenerateInformationMemorandumHtml(
        CirpCase cirp,
        List<CocMember> coc,
        List<AssetValuationItem> valuations,
        List<PufeTransactionItem> pufe
    )
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\" />");
        sb.AppendLine("<title>INFORMATION MEMORANDUM - REGULATION 36 OF IBBI (CIRP) REGULATIONS, 2016</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Times New Roman', Times, serif; font-size: 11pt; line-height: 1.6; color: #111; margin: 40px; }");
        sb.AppendLine("h1, h2, h3, h4 { color: #0f172a; margin-top: 18px; margin-bottom: 6px; }");
        sb.AppendLine("h1 { font-size: 16pt; text-align: center; text-transform: uppercase; border-bottom: 2px solid #0f172a; padding-bottom: 8px; }");
        sb.AppendLine("h2 { font-size: 13pt; border-bottom: 1px solid #94a3b8; padding-bottom: 4px; margin-top: 24px; }");
        sb.AppendLine("h3 { font-size: 11pt; font-weight: bold; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin: 12px 0; }");
        sb.AppendLine("th, td { border: 1px solid #94a3b8; padding: 6px 10px; font-size: 10pt; text-align: left; }");
        sb.AppendLine("th { background-color: #f1f5f9; font-weight: bold; }");
        sb.AppendLine(".banner { background: #f8fafc; border: 1px solid #cbd5e1; padding: 12px; margin: 12px 0; border-radius: 4px; }");
        sb.AppendLine(".confidential { text-align: center; color: #dc2626; font-weight: bold; font-size: 12pt; letter-spacing: 2px; }");
        sb.AppendLine(".signature { margin-top: 35px; border-top: 1px solid #333; width: 300px; padding-top: 5px; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine("<div class=\"confidential\">CONFIDENTIAL - STRICTLY FOR RESOLUTION APPLICANTS</div>");
        sb.AppendLine("<h1>INFORMATION MEMORANDUM</h1>");
        sb.AppendLine("<p style=\"text-align: center; font-size: 11pt;\"><b>Issued under Regulation 36 of the Insolvency and Bankruptcy Board of India (Insolvency Resolution Process for Corporate Persons) Regulations, 2016</b></p>");

        // Corporate Debtor Profile
        sb.AppendLine("<div class=\"banner\">");
        sb.AppendLine($"<p><b>Name of Corporate Debtor:</b> {cirp.CorporateDebtorName} (CIN: {cirp.Cin})</p>");
        sb.AppendLine($"<p><b>Adjudicating Authority:</b> {cirp.NcltBench} | <b>Order No.:</b> {cirp.CourtOrderNumber}</p>");
        sb.AppendLine($"<p><b>Insolvency Commencement Date (ICD):</b> {cirp.AdmissionDate:dd/MM/yyyy}</p>");
        sb.AppendLine($"<p><b>Resolution Professional:</b> {cirp.RpName} (IBBI Reg: {cirp.RpIpRegNumber})</p>");
        sb.AppendLine("</div>");

        // Section 1: Claims & Creditor Profile
        sb.AppendLine("<h2>1. LIST OF ADMITTED CLAIMS OF CREDITORS (REGULATION 36(2)(b))</h2>");
        sb.AppendLine($"<p><b>Total Admitted Financial Debt:</b> ₹{cirp.TotalAdmittedFinancialDebt:N2} Cr | <b>Total Admitted Operational Debt:</b> ₹{cirp.TotalAdmittedOperationalDebt:N2} Cr</p>");
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr><th>S.No.</th><th>Creditor Name</th><th>Category</th><th>Admitted Claim (₹ Cr)</th><th>Voting Share %</th><th>Security Interest</th></tr></thead><tbody>");
        int cIndex = 1;
        foreach (var c in coc)
        {
            sb.AppendLine($"<tr><td>{cIndex++}</td><td><b>{c.CreditorName}</b> {(c.IsRelatedParty ? "[Related Party - Disqualified]" : "")}</td><td>{c.Category}</td><td>₹{c.AdmittedClaimAmount:N2}</td><td><b>{c.VotingSharePct:F2}%</b></td><td>{c.SecurityInterestDescription}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        // Section 2: Asset Valuation & Liquidation Value
        sb.AppendLine("<h2>2. ASSETS AND LIABILITIES & REGISTERED VALUATION (REGULATION 35 & 36(2)(a))</h2>");
        decimal totalFair = valuations.Sum(v => v.AverageFairValue);
        decimal totalLiq = valuations.Sum(v => v.AverageLiquidationValue);
        sb.AppendLine($"<p><b>Estimated Total Fair Value:</b> ₹{totalFair:N2} Cr | <b>Estimated Total Liquidation Value:</b> ₹{totalLiq:N2} Cr</p>");
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr><th>Asset Class</th><th>Description</th><th>Valuer 1 (Fair / Liq)</th><th>Valuer 2 (Fair / Liq)</th><th>Average Fair Value</th><th>Average Liquidation Value</th></tr></thead><tbody>");
        foreach (var v in valuations)
        {
            sb.AppendLine($"<tr><td><b>{v.AssetClass}</b></td><td>{v.AssetDescription}</td><td>₹{v.Valuer1FairValue:N2} / ₹{v.Valuer1LiquidationValue:N2}</td><td>₹{v.Valuer2FairValue:N2} / ₹{v.Valuer2LiquidationValue:N2}</td><td><b>₹{v.AverageFairValue:N2} Cr</b></td><td><b>₹{v.AverageLiquidationValue:N2} Cr</b></td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        // Section 3: Forensic Audit Findings (PUFE Transactions)
        sb.AppendLine("<h2>3. FORENSIC AUDIT OF AVOIDANCE TRANSACTIONS (SECTIONS 43, 45, 50, 66)</h2>");
        if (pufe.Count > 0)
        {
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Section</th><th>Transaction Date</th><th>Counterparty</th><th>Transaction Amount</th><th>Estimated Avoidance</th><th>NCLT Application</th></tr></thead><tbody>");
            foreach (var p in pufe)
            {
                sb.AppendLine($"<tr><td><b>{p.PufeType}</b></td><td>{p.TransactionDate:dd/MM/yyyy}</td><td>{p.CounterpartyName} {(p.IsRelatedParty ? "[RP]" : "")}</td><td>₹{p.TransactionAmount:N2} Cr</td><td><b>₹{p.EstimatedAvoidanceRecoverableAmount:N2} Cr</b></td><td>{(p.NcltApplicationFiled ? "FILED U/S 35A" : "Under Examination")}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }
        else
        {
            sb.AppendLine("<p>No avoidance transactions identified within statutory look-back periods to date.</p>");
        }

        // Section 4: Resolution Professional Certification
        sb.AppendLine("<br/>");
        sb.AppendLine("<div class=\"signature\">");
        sb.AppendLine($"<b>{cirp.RpName}</b><br/>");
        sb.AppendLine($"Resolution Professional in the matter of {cirp.CorporateDebtorName}<br/>");
        sb.AppendLine($"IBBI Registration No.: {cirp.RpIpRegNumber}<br/>");
        sb.AppendLine($"Date: {DateTime.UtcNow:dd/MM/yyyy}");
        sb.AppendLine("</div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }
}
