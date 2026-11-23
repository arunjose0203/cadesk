using System.Text.Json;
using System.Text.RegularExpressions;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.TaxNotices;

public enum PrecedentCategory
{
    JurisdictionAndLimitation = 1,
    NaturalJusticeAndFairHearing = 2,
    ClericalAndBonaFideError = 3,
    TdsCreditMismatch = 4,
    ReassessmentValiditySec148 = 5,
    CashCreditsSec68 = 6,
    GstItcBonaFideBuyer = 7,
    GstRule88Discrepancy = 8,
    PenaltyReliefSec270A = 9,
    GeneralPrinciples = 10
}

public enum DefenseDraftStatus
{
    Draft = 1,
    UnderReview = 2,
    PartnerApproved = 3,
    Finalized = 4
}

public class StatutoryPrecedent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? FirmId { get; set; } // Null for platform seeds, non-null for firm-custom
    public TaxAuthority Authority { get; set; } = TaxAuthority.IncomeTax;
    public PrecedentCategory Category { get; set; } = PrecedentCategory.GeneralPrinciples;
    public string CaseTitle { get; set; } = "";
    public string Citation { get; set; } = "";
    public string CourtBench { get; set; } = "";
    public int JudgmentYear { get; set; } = 2024;
    public string ApplicableSections { get; set; } = "";
    public string KeyRatioDecidendi { get; set; } = "";
    public string DefenseGroundTemplate { get; set; } = "";
    public bool IsLandmark { get; set; } = true;
    public string Keywords { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class FactualItem
{
    public int ItemNo { get; set; }
    public string EventDate { get; set; } = "";
    public string Description { get; set; } = "";
    public string AnnexureRef { get; set; } = "";
}

public class MeritGroundItem
{
    public int GroundNo { get; set; }
    public string Title { get; set; } = "";
    public string Argument { get; set; } = "";
    public string RelevantSection { get; set; } = "";
    public string? CitedPrecedent { get; set; }
}

public class CitedPrecedentItem
{
    public Guid? PrecedentId { get; set; }
    public string CaseTitle { get; set; } = "";
    public string Citation { get; set; } = "";
    public string CourtBench { get; set; } = "";
    public string RelevantHolding { get; set; } = "";
}

public class AnnexureItem
{
    public string AnnexureNo { get; set; } = "";
    public string Title { get; set; } = "";
    public string DocumentType { get; set; } = "";
    public int PageCount { get; set; } = 1;
}

public class TaxDefenseDraft : TenantEntity, ISoftDelete
{
    public Guid NoticeId { get; set; }
    public TaxNotice? Notice { get; set; }

    public Guid ClientId { get; set; }
    public string DraftNumber { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subject { get; set; } = "";
    public string AddressedTo { get; set; } = "";
    public string ClientName { get; set; } = "";
    public string ClientPanOrGstin { get; set; } = "";
    public string AssessmentYear { get; set; } = "";
    public string FinancialYear { get; set; } = "";
    public string NoticeRefNumber { get; set; } = "";
    public string NoticeDin { get; set; } = "";
    public DateOnly NoticeIssueDate { get; set; }
    public DateOnly NoticeDueDate { get; set; }

    public string PreliminaryObjectionsJson { get; set; } = "[]";
    public string FactualMatrixJson { get; set; } = "[]";
    public string MeritGroundsJson { get; set; } = "[]";
    public string CitedPrecedentsJson { get; set; } = "[]";
    public string PrayerForRelief { get; set; } = "";
    public string AnnexureIndexJson { get; set; } = "[]";

    public DefenseDraftStatus Status { get; set; } = DefenseDraftStatus.Draft;
    public Guid? PreparedByUserId { get; set; }
    public string? PreparedByName { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? PartnerUdin { get; set; }
    public string? GeneratedHtml { get; set; }

    public DateTime? DeletedAt { get; set; }

    public List<string> GetPreliminaryObjections() =>
        string.IsNullOrWhiteSpace(PreliminaryObjectionsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(PreliminaryObjectionsJson) ?? new List<string>();

    public void SetPreliminaryObjections(List<string> items) =>
        PreliminaryObjectionsJson = JsonSerializer.Serialize(items);

    public List<FactualItem> GetFactualMatrix() =>
        string.IsNullOrWhiteSpace(FactualMatrixJson)
            ? new List<FactualItem>()
            : JsonSerializer.Deserialize<List<FactualItem>>(FactualMatrixJson) ?? new List<FactualItem>();

    public void SetFactualMatrix(List<FactualItem> items) =>
        FactualMatrixJson = JsonSerializer.Serialize(items);

    public List<MeritGroundItem> GetMeritGrounds() =>
        string.IsNullOrWhiteSpace(MeritGroundsJson)
            ? new List<MeritGroundItem>()
            : JsonSerializer.Deserialize<List<MeritGroundItem>>(MeritGroundsJson) ?? new List<MeritGroundItem>();

    public void SetMeritGrounds(List<MeritGroundItem> items) =>
        MeritGroundsJson = JsonSerializer.Serialize(items);

    public List<CitedPrecedentItem> GetCitedPrecedents() =>
        string.IsNullOrWhiteSpace(CitedPrecedentsJson)
            ? new List<CitedPrecedentItem>()
            : JsonSerializer.Deserialize<List<CitedPrecedentItem>>(CitedPrecedentsJson) ?? new List<CitedPrecedentItem>();

    public void SetCitedPrecedents(List<CitedPrecedentItem> items) =>
        CitedPrecedentsJson = JsonSerializer.Serialize(items);

    public List<AnnexureItem> GetAnnexureIndex() =>
        string.IsNullOrWhiteSpace(AnnexureIndexJson)
            ? new List<AnnexureItem>()
            : JsonSerializer.Deserialize<List<AnnexureItem>>(AnnexureIndexJson) ?? new List<AnnexureItem>();

    public void SetAnnexureIndex(List<AnnexureItem> items) =>
        AnnexureIndexJson = JsonSerializer.Serialize(items);
}

/// <summary>
/// Intelligent OCR Text Ingestion & Notice Categorizer Engine
/// </summary>
public static class NoticeOcrParser
{
    private static readonly Regex DinRegex = new(
        @"(?:DIN\s*[:\-]?\s*|DIN\s+NO\s*[:\-]?\s*)([A-Z0-9\/\-\(\)\.]{10,45})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PanRegex = new(
        @"\b([A-Z]{5}[0-9]{4}[A-Z]{1})\b",
        RegexOptions.Compiled);

    private static readonly Regex GstinRegex = new(
        @"\b([0-9]{2}[A-Z]{5}[0-9]{4}[A-Z]{1}[1-9A-Z]{1}Z[0-9A-Z]{1})\b",
        RegexOptions.Compiled);

    private static readonly Regex AyRegex = new(
        @"(?:A\.?Y\.?|Assessment\s+Year)\s*[:\-]?\s*(\d{4}[-\/]\d{2,4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FyRegex = new(
        @"(?:F\.?Y\.?|Financial\s+Year)\s*[:\-]?\s*(\d{4}[-\/]\d{2,4})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DemandRegex = new(
        @"(?:Demand|Amount\s+payable|Tax\s+payable|Disallowance|Addition|under\s+dispute|proposed\s+addition|proposed\s+penalty|penalty|excess\s+itc|exceeds[^\n\r]*?by|difference\s+of)\s*(?:of|by)?\s*(?:Rs\.?|INR|₹)?\s*([\d,]+(?:\.\d{2})?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public record ParseResult(
        TaxAuthority Authority,
        NoticeCategory Category,
        string Section,
        string? Din,
        DinStatus DinStatus,
        string? PanOrGstin,
        string? AssessmentYear,
        string? FinancialYear,
        DateOnly? IssueDate,
        DateOnly? DueDate,
        decimal DemandAmount,
        string OfficerJurisdiction,
        string ExtractedIssue,
        string SummaryOfAllegations,
        List<string> RecommendedPreliminaryObjections,
        List<string> RecommendedGrounds,
        int ConfidenceScore
    );

    public static ParseResult Parse(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return new ParseResult(
                TaxAuthority.IncomeTax,
                NoticeCategory.GeneralNotice,
                "General Notice",
                null,
                DinStatus.Unverified,
                null,
                null,
                null,
                null,
                null,
                0,
                "Assessing Authority",
                "Unspecified Tax Notice",
                "Please provide notice text for automated analysis.",
                new List<string> { "Lack of jurisdiction or proper sanction under the relevant statute." },
                new List<string> { "Notice lacks material particulars and fails to establish legal grounds for addition/demand." },
                10
            );
        }

        var text = rawText.Trim();
        var confidence = 30;

        // 1. Determine Authority: GST vs Income Tax
        var isGst = Regex.IsMatch(text, @"\b(GST|CGST|SGST|IGST|DRC-?01|ASMT-?10|GSTR-?[123B]+|Rule\s*88[CD]|Section\s*7[34])\b", RegexOptions.IgnoreCase);
        var authority = isGst ? TaxAuthority.Gst : TaxAuthority.IncomeTax;

        // 2. Identify Section & Notice Category
        var category = NoticeCategory.GeneralNotice;
        var section = isGst ? "Section 73" : "Section 142(1)";
        var extractedIssue = "General Statutory Inquiry";

        if (Regex.IsMatch(text, @"148A\(b\)|148A|148\b", RegexOptions.IgnoreCase))
        {
            authority = TaxAuthority.IncomeTax;
            category = NoticeCategory.Reassessment148_148A;
            section = text.Contains("148A(b)") ? "Section 148A(b)" : (text.Contains("148A") ? "Section 148A" : "Section 148");
            extractedIssue = "Reassessment on alleged income escaping assessment (High-value transaction/Insight portal data)";
            confidence += 25;
        }
        else if (Regex.IsMatch(text, @"143\(1\)|intimation\s+under\s+section\s+143", RegexOptions.IgnoreCase))
        {
            authority = TaxAuthority.IncomeTax;
            category = NoticeCategory.Intimation143_1;
            section = "Section 143(1)";
            extractedIssue = "Intimation adjustments (TDS 26AS mismatch or Chapter VI-A deduction disallowance)";
            confidence += 25;
        }
        else if (Regex.IsMatch(text, @"143\(2\)", RegexOptions.IgnoreCase))
        {
            authority = TaxAuthority.IncomeTax;
            category = NoticeCategory.Inquiry142_1;
            section = "Section 143(2)";
            extractedIssue = "Selection for Complete / Limited Scrutiny Assessment under CASS";
            confidence += 25;
        }
        else if (Regex.IsMatch(text, @"142\(1\)", RegexOptions.IgnoreCase))
        {
            authority = TaxAuthority.IncomeTax;
            category = NoticeCategory.Inquiry142_1;
            section = "Section 142(1)";
            extractedIssue = "Preliminary Inquiry Questionnaire / Production of Accounts & Documents";
            confidence += 25;
        }
        else if (Regex.IsMatch(text, @"270A|penalty\s+under\s+section\s+270A", RegexOptions.IgnoreCase))
        {
            authority = TaxAuthority.IncomeTax;
            category = NoticeCategory.PenaltyNotice270A;
            section = "Section 270A";
            extractedIssue = "Penalty proceedings for Under-reporting or Misreporting of Income";
            confidence += 25;
        }
        else if (Regex.IsMatch(text, @"DRC-?01B|Rule\s*88C", RegexOptions.IgnoreCase))
        {
            authority = TaxAuthority.Gst;
            category = NoticeCategory.GstDrc01A;
            section = "Form DRC-01B (Rule 88C)";
            extractedIssue = "Liability mismatch: Output tax declared in GSTR-1 exceeds GSTR-3B";
            confidence += 25;
        }
        else if (Regex.IsMatch(text, @"DRC-?01C|Rule\s*88D", RegexOptions.IgnoreCase))
        {
            authority = TaxAuthority.Gst;
            category = NoticeCategory.GstDrc01A;
            section = "Form DRC-01C (Rule 88D)";
            extractedIssue = "Input Tax Credit mismatch: Excess ITC availed in GSTR-3B compared to GSTR-2B";
            confidence += 25;
        }
        else if (Regex.IsMatch(text, @"DRC-?01A", RegexOptions.IgnoreCase))
        {
            authority = TaxAuthority.Gst;
            category = NoticeCategory.GstDrc01A;
            section = "Form DRC-01A (Part A)";
            extractedIssue = "Intimation of tax ascertained as being payable before Show Cause Notice";
            confidence += 20;
        }
        else if (Regex.IsMatch(text, @"DRC-?01\b|Section\s*73|Section\s*74", RegexOptions.IgnoreCase))
        {
            authority = TaxAuthority.Gst;
            category = NoticeCategory.GstDrc01;
            section = text.Contains("74") ? "Section 74 (Form DRC-01)" : "Section 73 (Form DRC-01)";
            extractedIssue = "Determination of tax not paid, short paid or input tax credit wrongly availed";
            confidence += 20;
        }
        else if (Regex.IsMatch(text, @"ASMT-?10", RegexOptions.IgnoreCase))
        {
            authority = TaxAuthority.Gst;
            category = NoticeCategory.GstAsmt10;
            section = "Form ASMT-10 (Section 61)";
            extractedIssue = "Scrutiny of returns pointing out discrepancies";
            confidence += 20;
        }

        // 3. Extract DIN
        string? din = null;
        var dinMatch = DinRegex.Match(text);
        if (dinMatch.Success)
        {
            din = dinMatch.Groups[1].Value.Trim();
            confidence += 15;
        }
        else
        {
            var fallbackDinMatch = Regex.Match(text, @"\b(ITBA\/[A-Z0-9\/\-\(\)]+)\b", RegexOptions.IgnoreCase);
            if (fallbackDinMatch.Success)
            {
                din = fallbackDinMatch.Groups[1].Value.Trim();
                confidence += 15;
            }
        }

        var dinStatus = DinValidator.EvaluateDin(din, authority, DateOnly.FromDateTime(DateTime.UtcNow));

        // 4. Extract PAN or GSTIN
        string? panOrGstin = null;
        var gstinMatch = GstinRegex.Match(text);
        if (gstinMatch.Success)
        {
            panOrGstin = gstinMatch.Groups[1].Value.ToUpperInvariant();
            confidence += 10;
        }
        else
        {
            var panMatch = PanRegex.Match(text);
            if (panMatch.Success)
            {
                panOrGstin = panMatch.Groups[1].Value.ToUpperInvariant();
                confidence += 10;
            }
        }

        // 5. Extract Assessment Year & Financial Year
        string? ay = null;
        var ayMatch = AyRegex.Match(text);
        if (ayMatch.Success)
        {
            ay = ayMatch.Groups[1].Value.Trim();
            confidence += 10;
        }

        string? fy = null;
        var fyMatch = FyRegex.Match(text);
        if (fyMatch.Success)
        {
            fy = fyMatch.Groups[1].Value.Trim();
        }
        else if (!string.IsNullOrEmpty(ay) && ay.Length >= 7)
        {
            // Infer FY from AY e.g. AY 2023-24 -> FY 2022-23
            if (int.TryParse(ay[..4], out var ayStart))
            {
                var fyStart = ayStart - 1;
                var fyEnd = (ayStart % 100);
                fy = $"{fyStart}-{fyEnd:D2}";
            }
        }

        // 6. Extract Demand / Disallowance Amount
        decimal demandAmount = 0;
        var demandMatch = DemandRegex.Match(text);
        if (demandMatch.Success)
        {
            var rawAmount = demandMatch.Groups[1].Value.Replace(",", "").Trim();
            if (decimal.TryParse(rawAmount, out var parsedAmt))
            {
                demandAmount = parsedAmt;
                confidence += 10;
            }
        }

        if (demandAmount == 0)
        {
            var fallbackAmountMatch = Regex.Match(text, @"(?:Rs\.?|INR|₹)\s*([\d,]{4,}(?:\.\d{2})?)", RegexOptions.IgnoreCase);
            if (fallbackAmountMatch.Success && decimal.TryParse(fallbackAmountMatch.Groups[1].Value.Replace(",", "").Trim(), out var fbAmt))
            {
                demandAmount = fbAmt;
                confidence += 5;
            }
        }

        // 7. Extract Dates (Issue Date & Due Date)
        DateOnly? issueDate = null;
        DateOnly? dueDate = null;

        var dateMatches = Regex.Matches(text, @"\b(\d{1,2})[\/\-\.](\d{1,2})[\/\-\.](\d{4})\b");
        if (dateMatches.Count > 0)
        {
            foreach (Match m in dateMatches)
            {
                if (int.TryParse(m.Groups[1].Value, out var d) &&
                    int.TryParse(m.Groups[2].Value, out var mo) &&
                    int.TryParse(m.Groups[3].Value, out var y))
                {
                    try
                    {
                        var parsedDate = new DateOnly(y, mo, d);
                        if (issueDate is null)
                        {
                            issueDate = parsedDate;
                        }
                        else if (dueDate is null && parsedDate > issueDate)
                        {
                            dueDate = parsedDate;
                        }
                    }
                    catch
                    {
                        // Ignore invalid date tokens
                    }
                }
            }
        }

        issueDate ??= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7));
        dueDate ??= issueDate.Value.AddDays(15);

        // 8. Officer Jurisdiction
        var jurisdiction = authority == TaxAuthority.IncomeTax
            ? (text.Contains("NFAC") ? "National Faceless Assessment Centre (NFAC), Delhi" : "Assessment Unit / Income Tax Officer")
            : "Superintendent / Assistant Commissioner of Central / State Tax";

        // 9. Summary of Allegations
        var summary = $"Notice issued under {section} regarding alleged discrepancies / taxable additions";
        if (text.Contains("148A"))
        {
            summary = "Show cause u/s 148A(b) alleging escapement of income based on information flagged in the Insight portal / Risk Management Strategy (RMS).";
        }
        else if (text.Contains("Rule 88D") || text.Contains("DRC-01C"))
        {
            summary = "Intimation of excess ITC availed in Form GSTR-3B compared to ITC available in Form GSTR-2B exceeding permissible variance under Rule 88D.";
        }
        else if (text.Contains("Rule 88C") || text.Contains("DRC-01B"))
        {
            summary = "Intimation of difference in liability reported in GSTR-1 and that paid in GSTR-3B under Rule 88C.";
        }
        else if (text.Contains("143(1)"))
        {
            summary = "Prima facie adjustments under Section 143(1)(a) disallowing TDS credit or deductions claimed under Chapter VI-A.";
        }
        else if (text.Contains("270A"))
        {
            summary = "Show cause notice for levy of penalty u/s 270A for alleged under-reporting or misreporting of income.";
        }

        // 10. Generate Recommended Preliminary Objections & Grounds
        var preliminaryObjections = new List<string>();
        var recommendedGrounds = new List<string>();

        if (authority == TaxAuthority.IncomeTax)
        {
            if (dinStatus == DinStatus.Invalid)
            {
                preliminaryObjections.Add("Document Identification Number (DIN) Invalidity: Notice issued without a valid DIN is non-est and void ab initio in terms of CBDT Circular No. 19/2019.");
            }
            if (section.Contains("148"))
            {
                preliminaryObjections.Add("Jurisdiction & Limitation Bar u/s 149: The reassessment notice is time-barred as the 3-year statutory period has elapsed without meeting the Rs. 50 Lakh threshold supported by tangible material.");
                preliminaryObjections.Add("Lack of Prior Sanction u/s 151: Notice issued without obtaining valid sanction from the specified authority (CCIT/DGIT) makes entire reassessment jurisdiction defective.");
                preliminaryObjections.Add("Violation of Principles of Natural Justice: Underlying Insight reports and third-party statements relied upon by the department have not been furnished to the assessee.");
                recommendedGrounds.Add("No escapement of income: The impugned transaction was already recorded in regular books of accounts and reflected in audited financial statements.");
                recommendedGrounds.Add("Source of funds fully explained: All receipts are backed by banking channels, verifiable confirmations, and KYC records of counter-parties.");
            }
            else if (section.Contains("143(1)"))
            {
                preliminaryObjections.Add("Permissible Scope of Section 143(1)(a): Prima facie adjustments cannot adjudicate debatable issues or disallow claims without prior intimation.");
                recommendedGrounds.Add("TDS Credit Available u/s 199 r.w. Rule 37BA: Taxes were deducted at source and reflected in Form 26AS/AIS; credit cannot be denied to the deductor.");
                recommendedGrounds.Add("Chapter VI-A Eligibility: Deductions are supported by valid certificates and eligible investment receipts.");
            }
            else if (section.Contains("270A"))
            {
                preliminaryObjections.Add("Absence of Specific Charge: Notice fails to specify whether penalty is initiated for 'under-reporting' or 'misreporting', violating natural justice.");
                recommendedGrounds.Add("Bona Fide Explanation u/s 270A(6): Assessee offered bona fide explanation with full disclosure of all material facts; no intention to evade tax exists (Price Waterhouse Coopers SC).");
            }
            else
            {
                preliminaryObjections.Add("Statutory Right to Personal Hearing u/s 144B(6): Personal hearing through video conference is mandatory prior to any adverse draft assessment.");
                recommendedGrounds.Add("Complete reconciliation and documentary substantiation submitted to explain all queries.");
            }
        }
        else // GST
        {
            if (section.Contains("DRC-01C") || section.Contains("88D"))
            {
                preliminaryObjections.Add("Procedural Compliance: Reply filed within the prescribed 7-day period under Rule 88D of CGST Rules, 2017.");
                recommendedGrounds.Add("Timing Differences: Invoices uploaded by suppliers in Form GSTR-1 in subsequent tax periods but availed in GSTR-3B in accordance with Section 16(4).");
                recommendedGrounds.Add("Input Tax Credit on Inward Supplies Liable to Reverse Charge (RCM): Tax paid under Section 9(3) properly availed as ITC though not reflecting in Table 2B.");
                recommendedGrounds.Add("Precedent Ruling: Buyer cannot be penalized for seller's inadvertent delay in GSTR-1 return filing when tax invoice and bank payment proofs exist (Suncraft Energies SC affirmed).");
            }
            else if (section.Contains("DRC-01B") || section.Contains("88C"))
            {
                preliminaryObjections.Add("Timely filing of Part B explanation within 7 days of DRC-01B intimation.");
                recommendedGrounds.Add("Typographical error in GSTR-1 rectified in subsequent tax period's Table 9 amendment.");
                recommendedGrounds.Add("Credit Note adjustments or exempt supplies incorrectly categorized in Table 4 of GSTR-1.");
            }
            else
            {
                preliminaryObjections.Add("Mandatory Opportunity of Hearing u/s 75(4): Section 75(4) of CGST Act mandates granting an opportunity of personal hearing before passing any adverse order.");
                preliminaryObjections.Add("Limitation Bar u/s 73(10): Show cause notice issued beyond the statutory limitation period reckoned from the due date of the annual return.");
                recommendedGrounds.Add("Purchases are genuine, duly supported by e-Way bills, tax invoices, weighbridge slips, and bank payment receipts.");
                recommendedGrounds.Add("No fraud, suppression or wilful misstatement to invoke extended period of limitation or Section 74 penalties.");
            }
        }

        confidence = Math.Min(100, Math.Max(25, confidence));

        return new ParseResult(
            authority,
            category,
            section,
            din,
            dinStatus,
            panOrGstin,
            ay,
            fy,
            issueDate,
            dueDate,
            demandAmount,
            jurisdiction,
            extractedIssue,
            summary,
            preliminaryObjections,
            recommendedGrounds,
            confidence
        );
    }
}
