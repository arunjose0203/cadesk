using System.Net;
using CaDesk.Domain.Common;

namespace CaDesk.Domain.Audit;

/// <summary>One recorded change or sensitive action. Append-only: never updated or deleted by the app.</summary>
public class AuditLog : Entity
{
    public Guid? FirmId { get; set; }
    public Guid? ActorId { get; set; }
    public string ActorType { get; set; } = AuditActor.User;
    public string Entity { get; set; } = "";
    public string? EntityId { get; set; }
    public string Action { get; set; } = AuditAction.Update;
    public string? OldValues { get; set; }   // jsonb
    public string? NewValues { get; set; }   // jsonb
    public IPAddress? Ip { get; set; }
    public DateTime At { get; set; }
}

public static class AuditAction
{
    public const string Create = "create";
    public const string Update = "update";
    public const string Delete = "delete";
    public const string Revoke = "revoke";
    public const string Export = "export";
    public const string Download = "download";
    public const string Approve = "approve";
}

public static class AuditActor
{
    public const string User = "user";
    public const string ClientUser = "client_user";
    public const string System = "system";
}
