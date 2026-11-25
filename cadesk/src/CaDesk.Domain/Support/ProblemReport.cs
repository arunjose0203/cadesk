using CaDesk.Domain.Common;

namespace CaDesk.Domain.Support;

/// <summary>
/// A crash the app caught or a problem a user reported (Sprint 72). Kept per firm; the operator reads them
/// across firms with the owner database role during the pilot (docs/PILOT.md).
/// </summary>
public class ProblemReport : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid? UserId { get; set; }
    /// <summary>crash | feedback</summary>
    public string Kind { get; set; } = ProblemKinds.Feedback;
    public string Message { get; set; } = "";
    public string? Page { get; set; }
    public string? Detail { get; set; }
    public string? UserAgent { get; set; }
    public string? AppVersion { get; set; }
    /// <summary>Same crash from the same user within an hour counts here instead of adding rows.</summary>
    public int Occurrences { get; set; } = 1;
    /// <summary>open | resolved</summary>
    public string Status { get; set; } = "open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}

public static class ProblemKinds
{
    public const string Crash = "crash";
    public const string Feedback = "feedback";
}
