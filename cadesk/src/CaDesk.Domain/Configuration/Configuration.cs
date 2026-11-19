using CaDesk.Domain.Common;

namespace CaDesk.Domain.Configuration;

/// <summary>The six levels a setting can be set at. Lower number = broader.</summary>
public enum ConfigScope : short
{
    Platform = 0,
    Country = 1,
    Plan = 2,
    Firm = 3,
    Client = 4,
    User = 5,
}

/// <summary>One setting in the catalogue: its type, validation, default and how far down it may be overridden.</summary>
public class ConfigDefinition
{
    public string Key { get; set; } = "";
    public string Area { get; set; } = "";
    public string ValueType { get; set; } = "";
    public string JsonSchema { get; set; } = "{}";      // jsonb
    public string DefaultValue { get; set; } = "null";  // jsonb
    public ConfigScope LowestScope { get; set; }
    public bool PlanValueIsCap { get; set; }
    public string Labels { get; set; } = "{}";          // jsonb
    public string? HelpText { get; set; }               // jsonb
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>A value at one level. FirmId is null for platform, country and plan levels.</summary>
public class ConfigValue : Entity
{
    public string Key { get; set; } = "";
    public ConfigScope Scope { get; set; }
    public string ScopeRef { get; set; } = "";
    public Guid? FirmId { get; set; }
    public string Value { get; set; } = "null";         // jsonb
    public bool IsLocked { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ConfigChangeLog : Entity
{
    public Guid? FirmId { get; set; }
    public string Key { get; set; } = "";
    public ConfigScope Scope { get; set; }
    public string ScopeRef { get; set; } = "";
    public string? OldValue { get; set; }               // jsonb; null = was inherited
    public string? NewValue { get; set; }               // jsonb; null = reset to inherited
    public bool OldLocked { get; set; }
    public bool NewLocked { get; set; }
    public string? Reason { get; set; }
    public Guid? ChangedBy { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>An encrypted value (API key, password, certificate). Only the ciphertext is stored.</summary>
public class Secret : Entity, ITenantOwned
{
    public Guid FirmId { get; set; }
    public string Name { get; set; } = "";
    public string Ciphertext { get; set; } = "";
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RotatedAt { get; set; }
}
