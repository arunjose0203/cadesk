using CaDesk.Domain.Common;

namespace CaDesk.Domain.Compliance;

/// <summary>A filing or payment a country requires (GSTR-3B, TDS payment, UAE VAT ...) and its due-date rule. Platform data.</summary>
public class ComplianceType
{
    public string Code { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string Authority { get; set; } = "";
    public string? Description { get; set; }
    public string[] Frequencies { get; set; } = [];
    public string DefaultFrequency { get; set; } = Frequency.Monthly;
    /// <summary>Due = (period end month + DueMonthsAfter), day DueDay (31 = last day) ...</summary>
    public short DueMonthsAfter { get; set; }
    public short DueDay { get; set; }
    /// <summary>... or period end + DueDaysAfter days, when set.</summary>
    public short? DueDaysAfter { get; set; }
    /// <summary>JSON: exceptions keyed by the period's end month, e.g. {"3":{"monthsAfter":1,"day":30}}.</summary>
    public string PeriodOverrides { get; set; } = "{}";
    public bool ShiftOnHoliday { get; set; }
    /// <summary>JSON: {"taxId":"GSTIN","entityTypes":[...],"excludeEntityTypes":[...]}.</summary>
    public string AppliesWhen { get; set; } = "{}";
    public bool AutoApply { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>A government extension of one period's due date. Platform data.</summary>
public class ComplianceExtension
{
    public string TypeCode { get; set; } = "";
    public string PeriodKey { get; set; } = "";
    public DateOnly ExtendedDue { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

/// <summary>A public holiday (FirmId null, platform data) or a day the firm's office is closed.</summary>
public class Holiday : Entity
{
    public string CountryCode { get; set; } = "";
    public Guid? FirmId { get; set; }
    public DateOnly HolidayDate { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A client's difference from the rules: a compliance switched on/off, or its frequency / quarters / year.</summary>
public class ClientCompliance : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public string TypeCode { get; set; } = "";
    public bool IsEnabled { get; set; }
    public string? Frequency { get; set; }
    /// <summary>1, 2 or 3: quarters end in Jan/Apr/Jul/Oct, Feb/May/Aug/Nov or Mar/Jun/Sep/Dec.</summary>
    public short? QuarterEndMonth { get; set; }
    public short? FyStartMonth { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>One client's filing for one period, e.g. GSTR-3B for Oct 2026.</summary>
public class Deadline : TenantEntity
{
    public Guid ClientId { get; set; }
    public string TypeCode { get; set; } = "";
    public string PeriodKey { get; set; } = "";
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public DateOnly StatutoryDue { get; set; }
    public DateOnly DueDate { get; set; }
    public DateOnly InternalDue { get; set; }
    public string Status { get; set; } = DeadlineStatus.Pending;
    public Guid? AssignedUserId { get; set; }
    public DateOnly? FiledOn { get; set; }
    public string? AckNo { get; set; }
    public string? Remarks { get; set; }
}

public static class DeadlineStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Filed = "filed";
    public const string NotApplicable = "not_applicable";
    public static readonly string[] All = [Pending, InProgress, Filed, NotApplicable];
    public static bool IsOpen(string s) => s is Pending or InProgress;
}

public static class Frequency
{
    public const string Monthly = "monthly";
    public const string Quarterly = "quarterly";
    public const string Annual = "annual";
}
