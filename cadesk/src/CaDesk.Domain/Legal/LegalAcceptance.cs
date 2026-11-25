using CaDesk.Domain.Common;

namespace CaDesk.Domain.Legal;

/// <summary>A user's acceptance of one version of a legal document (Sprint 71, DPDP Act 2023).</summary>
public class LegalAcceptance : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid UserId { get; set; }
    public string Document { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

public static class LegalDocuments
{
    public const string Terms = "terms";
    public const string Privacy = "privacy";
    /// <summary>Data processing terms: the firm is the data fiduciary for its clients' data, CaDesk its processor.</summary>
    public const string Dpa = "dpa";

    /// <summary>Current versions. Change a version when the text changes: everyone is asked to accept again.</summary>
    public static readonly IReadOnlyDictionary<string, string> Current = new Dictionary<string, string>
    {
        [Terms] = "2026-11-24",
        [Privacy] = "2026-11-24",
        [Dpa] = "2026-11-24",
    };

    public static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
    {
        [Terms] = "/legal/terms.html",
        [Privacy] = "/legal/privacy.html",
        [Dpa] = "/legal/dpa.html",
    };

    public static readonly IReadOnlyDictionary<string, string> Titles = new Dictionary<string, string>
    {
        [Terms] = "Terms of Service",
        [Privacy] = "Privacy Notice",
        [Dpa] = "Data Processing Terms",
    };
}
