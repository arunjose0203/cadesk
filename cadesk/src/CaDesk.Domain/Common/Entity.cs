namespace CaDesk.Domain.Common;

/// <summary>Base for every entity: UUID v7 key, so inserts stay in index order.</summary>
public abstract class Entity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
}

/// <summary>Marks a table that belongs to one firm (tenant). Filtered automatically.</summary>
public interface ITenantOwned
{
    Guid FirmId { get; set; }
}

/// <summary>Who created and last changed a row; stamped automatically on save.</summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTime? UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

/// <summary>Rows are hidden instead of deleted; the retention job removes them later.</summary>
public interface ISoftDelete
{
    DateTime? DeletedAt { get; set; }
}

/// <summary>Convenience base for most firm-owned master data.</summary>
public abstract class TenantEntity : Entity, ITenantOwned, IAuditable
{
    public Guid FirmId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    /// <summary>PostgreSQL xmin, used for optimistic concurrency.</summary>
    public uint Version { get; set; }
}
