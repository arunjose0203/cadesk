using System.Text;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.TaxLitigation;

public enum AppealForum
{
    CitAppeals = 1,       // First Appellate Authority - Form 35 (Sec 246A / 249)
    Itat = 2,             // Income Tax Appellate Tribunal - Form 36 (Sec 253)
    HighCourt = 3,        // High Court - Section 260A Substantial Question of Law
    SupremeCourt = 4      // Supreme Court - Special Leave Petition (Art 136) / Civil Appeal
}

public enum AppealStatus
{
    Drafting = 1,
    PartnerReviewed = 2,
    Certified = 3,
    Filed = 4,
    HearingScheduled = 5,
    HeardReserved = 6,
    DisposedAllowed = 7,
    DisposedPartlyAllowed = 8,
    DisposedDismissed = 9
}

public enum GroundType
{
    General = 1,
    JurisdictionOrLimitation = 2,
    NaturalJustice = 3,
    MeritsAddition = 4,
    Alternative = 5,
    WithoutPrejudice = 6
}

public enum PaperBookEvidenceType
{
    PartLowerAuthoritiesRecord = 1, // Already on record before AO or CIT(A)
    Rule29AdditionalEvidence = 2     // Fresh evidence under ITAT Rule 29 requiring separate application
}

public class TaxAppeal : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? TaxNoticeId { get; set; }
    public string? NoticeReferenceNumber { get; set; }

    public AppealForum Forum { get; set; } = AppealForum.Itat;
    public string? AppealNumber { get; set; } // e.g. "ITA No. 1420/DEL/2026"
    public string AssessmentYear { get; set; } = "2024-25";
    public string FinancialYear { get; set; } = "2023-24";
    public string AssessingOfficerWardOrCircle { get; set; } = "";

    public string ImpugnedOrderSection { get; set; } = "Sec 143(3)";
    public string ImpugnedOrderNumber { get; set; } = "";
    public DateOnly ImpugnedOrderDate { get; set; }
    public DateOnly DateOfServiceOfOrder { get; set; } // Starts statutory limitation clock

    public DateOnly StatutoryFilingDeadline { get; set; }
    public decimal FilingFeeAmount { get; set; }
    public decimal AssessedTotalIncome { get; set; }
    public decimal ReturnedTotalIncome { get; set; }
    public decimal TotalDisputedAdditions { get; set; }
    public decimal DisputedTaxDemand { get; set; }

    public DateOnly? FilingDate { get; set; }
    public string? DinOrAckNumber { get; set; } // e-Filing Acknowledgement Number
    public AppealStatus Status { get; set; } = AppealStatus.Drafting;

    public bool DelayCondonationRequired { get; set; }
    public int DaysOfDelay { get; set; }
    public string? CondonationReason { get; set; }

    public string StatementOfFacts { get; set; } = "";
    public string ReliefSought { get; set; } = "";

    public string? PartnerMembershipNumber { get; set; }
    public string? PartnerCertifiedByName { get; set; }
    public DateTimeOffset? PartnerCertifiedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public List<AppealGroundItem> Grounds { get; set; } = new();
    public List<PaperBookDocument> PaperBookDocuments { get; set; } = new();
    public List<AppealJudicialCitation> Citations { get; set; } = new();
}

public class AppealGroundItem : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid TaxAppealId { get; set; }
    public TaxAppeal? TaxAppeal { get; set; }

    public int GroundNumber { get; set; }
    public GroundType GroundType { get; set; } = GroundType.MeritsAddition;
    public string Title { get; set; } = "";
    public string GroundText { get; set; } = "";
    public string StatutorySection { get; set; } = "";
    public decimal DisputedAdditionAmount { get; set; }
    public bool IsWithoutPrejudice { get; set; }
    public bool IsAlternativeGround { get; set; }
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class PaperBookDocument : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid TaxAppealId { get; set; }
    public TaxAppeal? TaxAppeal { get; set; }

    public int ItemNumber { get; set; }
    public DateOnly DocumentDate { get; set; }
    public string DescriptionOfDocument { get; set; } = "";
    public string Relevance { get; set; } = "";
    public PaperBookEvidenceType EvidenceType { get; set; } = PaperBookEvidenceType.PartLowerAuthoritiesRecord;
    public DateOnly? LowerAuthorityFilingDate { get; set; }

    public int PageNumberFrom { get; set; }
    public int PageNumberTo { get; set; }
    public int TotalPages => Math.Max(1, (PageNumberTo - PageNumberFrom) + 1);

    public bool IsCertified { get; set; } = true;
    public int OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class AppealJudicialCitation : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid TaxAppealId { get; set; }
    public TaxAppeal? TaxAppeal { get; set; }

    public Guid? PrecedentId { get; set; }
    public string CaseTitle { get; set; } = "";
    public string Citation { get; set; } = "";
    public string CourtOrTribunal { get; set; } = "";
    public int JudgmentYear { get; set; }
    public string StatutorySection { get; set; } = "";
    public string KeyRatioProposition { get; set; } = "";
    public string ParagraphReference { get; set; } = "";
    public string RelevanceToGround { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public static class TaxAppealStatutoryCalculator
{
    public record LimitationResult(
        DateOnly DeadlineDate,
        int AllowedDays,
        int DaysRemaining,
        bool IsExpired,
        bool DelayCondonationRequired,
        int DaysOfDelay
    );

    /// <summary>
    /// Computes statutory limitation deadline based on Income Tax Act sections:
    /// CIT(A): 30 days u/s 249(2)
    /// ITAT: 60 days u/s 253(3)
    /// High Court: 120 days u/s 260A(2)(a)
    /// </summary>
    public static LimitationResult CalculateLimitation(AppealForum forum, DateOnly dateOfService, DateOnly asOfDate)
    {
        int allowedDays = forum switch
        {
            AppealForum.CitAppeals => 30,
            AppealForum.Itat => 60,
            AppealForum.HighCourt => 120,
            AppealForum.SupremeCourt => 90,
            _ => 60
        };

        var deadline = dateOfService.AddDays(allowedDays);
        int dayDiff = deadline.DayNumber - asOfDate.DayNumber;

        bool isExpired = dayDiff < 0;
        int daysOfDelay = isExpired ? Math.Abs(dayDiff) : 0;

        return new LimitationResult(
            DeadlineDate: deadline,
            AllowedDays: allowedDays,
            DaysRemaining: dayDiff,
            IsExpired: isExpired,
            DelayCondonationRequired: isExpired,
            DaysOfDelay: daysOfDelay
        );
    }

    /// <summary>
    /// Computes mandatory statutory appeal filing fees:
    /// CIT(A) u/s 249(1):
    ///   - Assessed income <= 1L -> ₹250
    ///   - Assessed income 1L to 2L -> ₹500
    ///   - Assessed income > 2L -> ₹1,000
    ///   - Other matters -> ₹250
    /// ITAT u/s 253(6):
    ///   - Assessed income <= 1L -> ₹500
    ///   - Assessed income 1L to 2L -> ₹1,500
    ///   - Assessed income > 2L -> 1% of assessed income, subject to a cap of ₹10,000
    ///   - Other matters -> ₹500
    /// High Court / Supreme Court -> Court Fee Act rules (standard ₹5,000 / ₹10,000)
    /// </summary>
    public static decimal CalculateStatutoryFilingFee(AppealForum forum, decimal assessedTotalIncome, bool isOtherMatter = false)
    {
        if (isOtherMatter)
        {
            return forum switch
            {
                AppealForum.CitAppeals => 250m,
                AppealForum.Itat => 500m,
                AppealForum.HighCourt => 5000m,
                AppealForum.SupremeCourt => 10000m,
                _ => 500m
            };
        }

        switch (forum)
        {
            case AppealForum.CitAppeals:
                if (assessedTotalIncome <= 100000m) return 250m;
                if (assessedTotalIncome <= 200000m) return 500m;
                return 1000m;

            case AppealForum.Itat:
                if (assessedTotalIncome <= 100000m) return 500m;
                if (assessedTotalIncome <= 200000m) return 1500m;
                decimal onePercent = Math.Round(assessedTotalIncome * 0.01m, 0);
                return Math.Min(onePercent, 10000m);

            case AppealForum.HighCourt:
                return 5000m;

            case AppealForum.SupremeCourt:
                return 10000m;

            default:
                return 500m;
        }
    }
}

public static class ItatRule18PaperBookEngine
{
    /// <summary>
    /// Re-paginates documents sequentially according to ITAT Rule 18(1)
    /// </summary>
    public static void RepaginateDocuments(List<PaperBookDocument> docs)
    {
        var ordered = docs.OrderBy(d => d.OrderIndex).ThenBy(d => d.ItemNumber).ToList();
        int currentPage = 1;

        for (int i = 0; i < ordered.Count; i++)
        {
            var d = ordered[i];
            d.ItemNumber = i + 1;
            int pageCount = Math.Max(1, d.TotalPages);
            d.PageNumberFrom = currentPage;
            d.PageNumberTo = currentPage + pageCount - 1;
            currentPage = d.PageNumberTo + 1;
        }
    }

    /// <summary>
    /// Generates the mandatory true copy certificate under ITAT Rule 18(6)
    /// </summary>
    public static string GenerateRule18Certificate(
        string appellantName,
        string pan,
        string appealNumber,
        string assessmentYear,
        int totalPages,
        string partnerName,
        string membershipNo,
        string firmName
    )
    {
        return $@"CERTIFICATE UNDER RULE 18(6) OF THE INCOME TAX APPELLATE TRIBUNAL RULES, 1963

IN THE INCOME TAX APPELLATE TRIBUNAL

IN THE MATTER OF:
{appellantName} (PAN: {pan})
... Appellant
Versus
The Assessing Officer / Deputy Commissioner of Income Tax
... Respondent

Appeal No.: {appealNumber}
Assessment Year: {assessmentYear}

CERTIFICATE OF TRUTH AND ACCURACY

I, {partnerName}, Chartered Accountant (ICAI Membership No.: {membershipNo}), Partner of M/s {firmName}, Authorised Representative for the Appellant, do hereby certify that:

1. The documents contained in this Paper Book running from Page Nos. 1 to {totalPages} are true, correct, and complete copies of the original documents and evidence that formed part of the record before the lower authorities, namely the Assessing Officer and/or the Commissioner of Income Tax (Appeals).

2. Each document included in this Paper Book was duly produced before or filed with the Assessing Officer / Commissioner of Income Tax (Appeals) during the assessment / appellate proceedings, except where an explicit application for admission of additional evidence under Rule 29 has been specifically filed.

3. The paging of this Paper Book is consecutive and matches the Index of Documents preceding this Certificate.

Verified at ____________ on this _____ day of ____________ 2026.



_____________________________________________
({partnerName})
Chartered Accountant / Authorised Representative
ICAI Membership No.: {membershipNo}
For and on behalf of: {firmName}";
    }

    /// <summary>
    /// Compiles the complete Form 36 (ITAT) or Form 35 (CIT(A)) Appeal Dossier HTML
    /// </summary>
    public static string CompileAppealDossierHtml(
        TaxAppeal appeal,
        List<AppealGroundItem> grounds,
        List<PaperBookDocument> docs,
        List<AppealJudicialCitation> citations
    )
    {
        RepaginateDocuments(docs);
        int totalPages = docs.Count > 0 ? docs.Max(d => d.PageNumberTo) : 1;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\" />");
        sb.AppendLine("<title>Statutory Tax Appeal Dossier - " + (appeal.AppealNumber ?? appeal.AssessmentYear) + "</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: 'Times New Roman', serif; font-size: 11pt; line-height: 1.5; color: #111; margin: 40px; }");
        sb.AppendLine("h1, h2, h3 { text-align: center; text-transform: uppercase; margin: 6px 0; }");
        sb.AppendLine("h1 { font-size: 15pt; text-decoration: underline; }");
        sb.AppendLine("h2 { font-size: 13pt; }");
        sb.AppendLine("h3 { font-size: 11pt; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin: 16px 0; }");
        sb.AppendLine("th, td { border: 1px solid #444; padding: 6px 10px; font-size: 10pt; }");
        sb.AppendLine("th { background-color: #f2f2f2; text-align: left; }");
        sb.AppendLine(".ground-box { margin-bottom: 14px; page-break-inside: avoid; }");
        sb.AppendLine(".ground-title { font-weight: bold; margin-bottom: 4px; }");
        sb.AppendLine(".page-break { page-break-before: always; }");
        sb.AppendLine(".cert-box { border: 2px solid #333; padding: 20px; margin-top: 30px; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Forum Heading
        string forumTitle = appeal.Forum switch
        {
            AppealForum.CitAppeals => "FORM NO. 35<br/>[See Rule 45 of Income Tax Rules, 1962]<br/>MEMORANDUM OF APPEAL TO THE COMMISSIONER OF INCOME TAX (APPEALS)",
            AppealForum.Itat => "FORM NO. 36<br/>[See Rule 8(1) of Income Tax Appellate Tribunal Rules, 1963]<br/>MEMORANDUM OF APPEAL TO THE INCOME TAX APPELLATE TRIBUNAL",
            AppealForum.HighCourt => "IN THE HIGH COURT OF JUDICATURE<br/>APPEAL UNDER SECTION 260A OF THE INCOME TAX ACT, 1961",
            _ => "MEMORANDUM OF APPEAL"
        };

        sb.AppendLine($"<h1>{forumTitle}</h1>");
        sb.AppendLine($"<h2>ASSESSMENT YEAR: {appeal.AssessmentYear}</h2>");
        sb.AppendLine($"<h3>APPEAL NO.: {appeal.AppealNumber ?? "[PENDING ADMISSION]"}</h3>");

        // Particulars Table
        string pan = (appeal.Client?.TaxIds != null && appeal.Client.TaxIds.TryGetValue("PAN", out var p)) ? p : "Not Specified";
        sb.AppendLine("<table>");
        sb.AppendLine($"<tr><th>1. Name and Address of Appellant</th><td><b>{appeal.Client?.Name ?? "Appellant"}</b><br/>PAN: {pan}</td></tr>");
        sb.AppendLine($"<tr><th>2. The Respondent Authority</th><td>{appeal.AssessingOfficerWardOrCircle}</td></tr>");
        sb.AppendLine($"<tr><th>3. Assessment Year</th><td>{appeal.AssessmentYear} (Financial Year: {appeal.FinancialYear})</td></tr>");
        sb.AppendLine($"<tr><th>4. Section under which Order Passed</th><td>{appeal.ImpugnedOrderSection}</td></tr>");
        sb.AppendLine($"<tr><th>5. Order Number & Date</th><td>No.: {appeal.ImpugnedOrderNumber}<br/>Dated: {appeal.ImpugnedOrderDate:dd/MM/yyyy}</td></tr>");
        sb.AppendLine($"<tr><th>6. Date of Service of Order</th><td>{appeal.DateOfServiceOfOrder:dd/MM/yyyy}</td></tr>");
        sb.AppendLine($"<tr><th>7. Statutory Filing Deadline</th><td>{appeal.StatutoryFilingDeadline:dd/MM/yyyy} (Limitation: {(appeal.DelayCondonationRequired ? "Delay of " + appeal.DaysOfDelay + " days - Condonation Prayed" : "Within statutory limit")})</td></tr>");
        sb.AppendLine($"<tr><th>8. Assessed Total Income</th><td>₹{appeal.AssessedTotalIncome:N2}</td></tr>");
        sb.AppendLine($"<tr><th>9. Disputed Tax Demand</th><td>₹{appeal.DisputedTaxDemand:N2}</td></tr>");
        sb.AppendLine($"<tr><th>10. Statutory Appeal Filing Fee</th><td>₹{appeal.FilingFeeAmount:N2} (Paid via e-Challan)</td></tr>");
        sb.AppendLine("</table>");

        // Statement of Facts
        sb.AppendLine("<div class=\"page-break\"></div>");
        sb.AppendLine("<h2>STATEMENT OF FACTS</h2>");
        sb.AppendLine("<div style=\"text-align: justify;\">");
        sb.AppendLine(string.IsNullOrWhiteSpace(appeal.StatementOfFacts) ? "<p>1. The Appellant is an Indian entity who duly filed its Return of Income for the subject assessment year disclosing all taxable receipts.</p>" : "<p>" + appeal.StatementOfFacts.Replace("\n", "<br/>") + "</p>");
        sb.AppendLine("</div>");

        // Grounds of Appeal
        sb.AppendLine("<div class=\"page-break\"></div>");
        sb.AppendLine("<h2>GROUNDS OF APPEAL</h2>");
        sb.AppendLine("<p style=\"text-align: justify;\">The Appellant above named respectfully submits the following Grounds of Appeal against the Impugned Order, each of which is without prejudice to the other:</p>");

        int gIndex = 1;
        foreach (var g in grounds.OrderBy(x => x.OrderIndex).ThenBy(x => x.GroundNumber))
        {
            sb.AppendLine("<div class=\"ground-box\">");
            sb.AppendLine($"<div class=\"ground-title\">Ground {gIndex++}: {g.Title}</div>");
            sb.AppendLine($"<div style=\"text-align: justify;\">{g.GroundText}</div>");
            if (g.DisputedAdditionAmount > 0)
            {
                sb.AppendLine($"<div style=\"font-size: 10pt; color: #555;\">Disputed Addition u/s {g.StatutorySection}: ₹{g.DisputedAdditionAmount:N2}</div>");
            }
            sb.AppendLine("</div>");
        }

        // Relief Sought
        sb.AppendLine("<h3>PRAYER FOR RELIEF</h3>");
        sb.AppendLine("<div style=\"text-align: justify;\">");
        sb.AppendLine(string.IsNullOrWhiteSpace(appeal.ReliefSought) ? "<p>In light of the above Grounds of Appeal and Judicial Authorities cited, the Appellant prays that the illegal and uncalled for additions made in the Impugned Order be deleted in full, and statutory interest charged be cancelled.</p>" : "<p>" + appeal.ReliefSought.Replace("\n", "<br/>") + "</p>");
        sb.AppendLine("</div>");

        // ITAT Rule 18 Paper Book Table of Contents
        sb.AppendLine("<div class=\"page-break\"></div>");
        sb.AppendLine("<h2>PAPER BOOK INDEX UNDER ITAT RULE 18</h2>");
        sb.AppendLine($"<p><b>In the Appeal of:</b> {appeal.Client?.Name ?? "Appellant"} | <b>AY:</b> {appeal.AssessmentYear}</p>");
        sb.AppendLine("<table>");
        sb.AppendLine("<thead><tr><th style=\"width: 8%;\">S.No.</th><th style=\"width: 14%;\">Doc Date</th><th>Description of Document / Evidence</th><th style=\"width: 18%;\">Evidence Category</th><th style=\"width: 12%; text-align: center;\">Page Nos.</th></tr></thead>");
        sb.AppendLine("<tbody>");

        foreach (var d in docs.OrderBy(x => x.ItemNumber))
        {
            string cat = d.EvidenceType == PaperBookEvidenceType.PartLowerAuthoritiesRecord ? "Part of Lower Record" : "Rule 29 Addl. Evidence";
            sb.AppendLine($"<tr><td style=\"text-align: center;\">{d.ItemNumber}</td><td>{d.DocumentDate:dd/MM/yyyy}</td><td><b>{d.DescriptionOfDocument}</b><br/><span style=\"font-size: 9pt; color: #555;\">{d.Relevance}</span></td><td>{cat}</td><td style=\"text-align: center; font-weight: bold;\">{d.PageNumberFrom} – {d.PageNumberTo}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");

        // Judicial Authorities Index
        if (citations.Count > 0)
        {
            sb.AppendLine("<h3>ANNEXED JUDICIAL AUTHORITIES & LANDMARK RATIOS</h3>");
            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>S.No.</th><th>Case Title & Citation</th><th>Court / Tribunal</th><th>Statutory Section</th><th>Ratio Decidendi / Proposition</th></tr></thead><tbody>");
            int cIndex = 1;
            foreach (var c in citations)
            {
                sb.AppendLine($"<tr><td style=\"text-align: center;\">{cIndex++}</td><td><b>{c.CaseTitle}</b><br/>{c.Citation}</td><td>{c.CourtOrTribunal} ({c.JudgmentYear})</td><td>{c.StatutorySection}</td><td>{c.KeyRatioProposition} {(!string.IsNullOrEmpty(c.ParagraphReference) ? $"[Para {c.ParagraphReference}]" : "")}</td></tr>");
            }
            sb.AppendLine("</tbody></table>");
        }

        // Rule 18(6) Certificate
        sb.AppendLine("<div class=\"page-break\"></div>");
        string certPan = (appeal.Client?.TaxIds != null && appeal.Client.TaxIds.TryGetValue("PAN", out var cp)) ? cp : "Not Specified";
        string certText = GenerateRule18Certificate(
            appellantName: appeal.Client?.Name ?? "Appellant",
            pan: certPan,
            appealNumber: appeal.AppealNumber ?? "ITA No. [Draft]/DEL/2026",
            assessmentYear: appeal.AssessmentYear,
            totalPages: totalPages,
            partnerName: appeal.PartnerCertifiedByName ?? "Audit Partner",
            membershipNo: appeal.PartnerMembershipNumber ?? "504932",
            firmName: "CA Desk Legal & Tax Practice"
        );

        sb.AppendLine("<div class=\"cert-box\"><pre style=\"font-family: 'Times New Roman', serif; font-size: 11pt; white-space: pre-wrap;\">" + certText + "</pre></div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
