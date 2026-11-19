using CaDesk.Domain.Common;

namespace CaDesk.Domain.Clients;

/// <summary>A family or business group of clients (e.g. "Menon family", "Anand group of companies").</summary>
public class ClientGroup : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A person at the client: director, accountant, owner. At most one is primary.</summary>
public class ClientContact : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public string Name { get; set; } = "";
    public string? Role { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsPrimary { get; set; }
    public bool ReceivesReminders { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ClientNote : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public Guid ClientId { get; set; }
    public string Body { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>A firm-defined extra field on clients (and later other records).</summary>
public class CustomFieldDefinition : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public string Entity { get; set; } = "client";
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string FieldType { get; set; } = CustomFieldTypes.Text;
    public List<string>? Options { get; set; }
    public bool IsRequired { get; set; }
    public string? ValidationRegex { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class CustomFieldTypes
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Date = "date";
    public const string Dropdown = "dropdown";
    public const string Bool = "bool";
    public static readonly string[] All = [Text, Number, Date, Dropdown, Bool];
}

/// <summary>A tax identifier a country uses (PAN, GSTIN, TRN ...) with its format rule. Platform data.</summary>
public class TaxIdType
{
    public string CountryCode { get; set; } = "";
    public string Code { get; set; } = "";
    public string Label { get; set; } = "";
    public string Pattern { get; set; } = "";
    public string Checksum { get; set; } = "none";
    /// <summary>Two clients of one firm can't share this ID (e.g. GSTIN). Others (PAN) only warn.</summary>
    public bool IsUnique { get; set; }
    public string? Example { get; set; }
    public int SortOrder { get; set; }
}
