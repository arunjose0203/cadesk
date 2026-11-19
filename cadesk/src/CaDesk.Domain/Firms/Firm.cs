using CaDesk.Domain.Common;

namespace CaDesk.Domain.Firms;

/// <summary>A CA or accounting firm: the tenant. Created by sign-up (Sprint 2).</summary>
public class Firm : Entity
{
    public string Name { get; set; } = "";
    public string? LegalName { get; set; }
    public string CountryCode { get; set; } = "IN";
    public string DataRegion { get; set; } = "in";
    public string BaseCurrency { get; set; } = "INR";
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public string PlanCode { get; set; } = "STARTER";
    public string Status { get; set; } = FirmStatus.Trial;
    public string? Subdomain { get; set; }
    public Dictionary<string, string> TaxIds { get; set; } = new();
    public int ConfigVersion { get; set; } = 1;
    public DateTime? TrialEndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public static class FirmStatus
{
    public const string Trial = "trial";
    public const string Active = "active";
    public const string Suspended = "suspended";
    public const string Closed = "closed";
}
