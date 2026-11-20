using System.Text.RegularExpressions;
using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.TaxNotices;

public enum TaxAuthority
{
    IncomeTax = 1,
    Gst = 2,
    Customs = 3,
    Other = 4
}

public enum NoticeCategory
{
    Intimation143_1 = 1,
    DefectiveReturn139_9 = 2,
    Inquiry142_1 = 3,
    Reassessment148_148A = 4,
    DemandNotice156 = 5,
    RefundAdjustment245 = 6,
    PenaltyNotice270A = 7,
    GstDrc01A = 8,
    GstDrc01 = 9,
    GstAsmt10 = 10,
    GstReg17 = 11,
    GstDrc07 = 12,
    GeneralNotice = 99
}

public enum NoticeStatus
{
    Received = 1,
    InReview = 2,
    DraftPrepared = 3,
    PartnerApproved = 4,
    Submitted = 5,
    DisposedOrClosed = 6,
    Appealed = 7
}

public enum DinStatus
{
    Unverified = 0,
    Valid = 1,
    Invalid = 2,
    NotApplicable = 3
}

public enum HearingType
{
    WrittenSubmission = 1,
    VideoConference = 2,
    PhysicalHearing = 3
}

public enum HearingStatus
{
    Scheduled = 1,
    Attended = 2,
    AdjournmentRequested = 3,
    Concluded = 4,
    Cancelled = 5
}

public enum ResponseStatus
{
    Draft = 1,
    UnderReview = 2,
    PartnerApproved = 3,
    UploadedToPortal = 4
}

public enum AppealStatus
{
    NotAppealed = 1,
    AppealDrafted = 2,
    FiledCitA = 3,
    StayGranted = 4,
    DemandDeleted = 5,
    Settled = 6
}

public class TaxNotice : TenantEntity, ISoftDelete
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public TaxAuthority Authority { get; set; } = TaxAuthority.IncomeTax;
    public NoticeCategory Category { get; set; } = NoticeCategory.GeneralNotice;
    public string Section { get; set; } = "";
    public string NoticeNumber { get; set; } = "";
    public string? Din { get; set; }
    public DinStatus DinStatus { get; set; } = DinStatus.Unverified;
    public string? AssessmentYear { get; set; }
    public string FinancialYear { get; set; } = "";
    public DateOnly IssueDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal DemandAmount { get; set; }
    public decimal RefundDisallowed { get; set; }
    public NoticeStatus Status { get; set; } = NoticeStatus.Received;
    public string Subject { get; set; } = "";
    public string? Description { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToName { get; set; }
    public string? OfficerName { get; set; }
    public string? OfficerJurisdiction { get; set; }

    public DateTime? DeletedAt { get; set; }

    public List<NoticeHearing> Hearings { get; set; } = new();
    public List<NoticeResponse> Responses { get; set; } = new();
    public List<TaxDemand> Demands { get; set; } = new();
}

public class NoticeHearing : TenantEntity
{
    public Guid NoticeId { get; set; }
    public TaxNotice? Notice { get; set; }

    public DateTimeOffset HearingDate { get; set; }
    public HearingType HearingType { get; set; } = HearingType.WrittenSubmission;
    public string? OfficerName { get; set; }
    public string? OfficerDesignation { get; set; }
    public string? MeetingLink { get; set; }
    public HearingStatus Status { get; set; } = HearingStatus.Scheduled;
    public string? AdjournmentReason { get; set; }
    public string? SummaryNotes { get; set; }

    public Guid? AttendedByUserId { get; set; }
    public string? AttendedByName { get; set; }
}

public class NoticeResponse : TenantEntity
{
    public Guid NoticeId { get; set; }
    public TaxNotice? Notice { get; set; }

    public int ResponseVersion { get; set; } = 1;
    public string SubmissionTitle { get; set; } = "";
    public string SummaryOfSubmissions { get; set; } = "";
    public string? LegalGrounds { get; set; }
    public ResponseStatus Status { get; set; } = ResponseStatus.Draft;

    public Guid? PreparedByUserId { get; set; }
    public string? PreparedByName { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }

    public string? PortalAckNumber { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public string? AttachmentNames { get; set; }
}

public class TaxDemand : TenantEntity
{
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }

    public Guid? NoticeId { get; set; }
    public TaxNotice? Notice { get; set; }

    public string? AssessmentYear { get; set; }
    public string FinancialYear { get; set; } = "";
    public string Section { get; set; } = "";
    public decimal OriginalDemand { get; set; }
    public decimal StayedAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal AdjustedRefund { get; set; }
    public DateOnly DemandDate { get; set; }
    public DateOnly AppealDueDate { get; set; }
    public AppealStatus AppealStatus { get; set; } = AppealStatus.NotAppealed;
    public string? AppealAckNumber { get; set; }
    public string? Remarks { get; set; }

    public decimal OutstandingAmount =>
        Math.Max(0, OriginalDemand - StayedAmount - PaidAmount - AdjustedRefund);

    public int DaysUntilAppealDue(DateOnly asOf) =>
        AppealDueDate.DayNumber - asOf.DayNumber;
}

public static class DinValidator
{
    private static readonly DateOnly ItDinMandatoryDate = new(2019, 10, 1);
    private static readonly DateOnly GstDinMandatoryDate = new(2019, 11, 8);

    private static readonly Regex StandardDinRegex = new(@"^[A-Z0-9\/\-\(\)\.]{10,50}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Validates DIN presence and format per CBDT Circular No. 19/2019 and CBIC Circular No. 122/41/2019.
    /// Communications issued on or after the effective date without a DIN are invalid in law (non-est).
    /// </summary>
    public static DinStatus EvaluateDin(string? din, TaxAuthority authority, DateOnly issueDate)
    {
        var isPostMandate = authority switch
        {
            TaxAuthority.IncomeTax => issueDate >= ItDinMandatoryDate,
            TaxAuthority.Gst => issueDate >= GstDinMandatoryDate,
            _ => false
        };

        if (string.IsNullOrWhiteSpace(din))
        {
            return isPostMandate ? DinStatus.Invalid : DinStatus.NotApplicable;
        }

        var trimmed = din.Trim();
        if (StandardDinRegex.IsMatch(trimmed))
        {
            return DinStatus.Valid;
        }

        return DinStatus.Invalid;
    }
}

public static class AppealDeadlineCalculator
{
    /// <summary>
    /// Calculates statutory appeal deadline.
    /// Under IT Act Sec 249(2), appeal before CIT(A) must be filed within 30 days of receipt of Notice of Demand (Sec 156).
    /// Under CGST Act Sec 107(1), appeal before Appellate Authority must be filed within 3 months of communication of order.
    /// </summary>
    public static DateOnly CalculateAppealDueDate(TaxAuthority authority, DateOnly demandDate)
    {
        return authority switch
        {
            TaxAuthority.IncomeTax => demandDate.AddDays(30),
            TaxAuthority.Gst => demandDate.AddMonths(3),
            _ => demandDate.AddDays(30)
        };
    }
}
