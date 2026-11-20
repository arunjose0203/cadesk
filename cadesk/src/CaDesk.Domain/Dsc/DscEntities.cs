using CaDesk.Domain.Clients;
using CaDesk.Domain.Common;
using CaDesk.Domain.Users;

namespace CaDesk.Domain.Dsc;

public static class DscLocation
{
    public const string Vault = "vault";
    public const string WithStaff = "with_staff";
    public const string WithClient = "with_client";
    public const string InTransit = "in_transit";

    public static readonly string[] All = [Vault, WithStaff, WithClient, InTransit];
}

public static class DscStatus
{
    public const string Active = "active";
    public const string ExpiringSoon = "expiring_soon";
    public const string Expired = "expired";
    public const string Revoked = "revoked";
    public const string HandedOver = "handed_over";

    public static readonly string[] All = [Active, ExpiringSoon, Expired, Revoked, HandedOver];
}

public static class DscType
{
    public const string Class3 = "class_3";
    public const string Dgft = "dgft";
    public const string Other = "other";

    public static readonly string[] All = [Class3, Dgft, Other];
}

/// <summary>
/// Digital Signature Certificate (DSC) and physical ePass/ProxKey token register.
/// </summary>
public class DscRegister : TenantEntity
{
    public Guid? ClientId { get; set; }
    public string HolderName { get; set; } = "";
    public string Pan { get; set; } = "";
    public string DscType { get; set; } = Dsc.DscType.Class3;
    public string IssuingAuthority { get; set; } = "eMudhra";
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
    public string? SerialNumber { get; set; }
    public string? UsbTokenId { get; set; }
    public string Location { get; set; } = DscLocation.Vault;
    public Guid? CustodianUserId { get; set; }
    public string? PinProtected { get; set; }
    public string Status { get; set; } = DscStatus.Active;
    public string? Notes { get; set; }

    public Client? Client { get; set; }
    public User? CustodianUser { get; set; }
    public List<DscMovementLog> MovementLogs { get; set; } = new();
}

/// <summary>
/// Audit trail of physical DSC token movements between safe vault, staff, and clients.
/// </summary>
public class DscMovementLog : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid DscId { get; set; }
    public DateTime MovedAt { get; set; } = DateTime.UtcNow;
    public string FromLocation { get; set; } = "";
    public string ToLocation { get; set; } = "";
    public Guid? HandlerUserId { get; set; }
    public string? Remarks { get; set; }
    public Guid? CreatedBy { get; set; }

    public User? HandlerUser { get; set; }
}
