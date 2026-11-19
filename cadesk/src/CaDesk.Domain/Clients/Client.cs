using CaDesk.Domain.Common;

namespace CaDesk.Domain.Clients;

/// <summary>A customer of the firm. Every deadline, document and computation hangs from it.</summary>
public class Client : TenantEntity, ISoftDelete
{
    public Guid? BranchId { get; set; }
    public Guid? GroupId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? LegalName { get; set; }
    public string CountryCode { get; set; } = "IN";
    public string EntityType { get; set; } = "Individual";

    /// <summary>PAN, GSTIN, TAN (India) / TRN, VAT no., CR no. (GCC).</summary>
    public Dictionary<string, string> TaxIds { get; set; } = new();

    public Guid? AssignedUserId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }

    /// <summary>Values for the firm's custom fields (Sprint 5).</summary>
    public Dictionary<string, string> Custom { get; set; } = new();

    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }
}
