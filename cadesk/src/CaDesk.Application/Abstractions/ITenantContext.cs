namespace CaDesk.Application.Abstractions;

/// <summary>
/// The firm and user the current request (or background job) acts for.
/// Set once per scope by TenantMiddleware or by a job before it touches data.
/// </summary>
public interface ITenantContext
{
    bool HasTenant { get; }
    Guid FirmId { get; }
    Guid? UserId { get; }
    string? Role { get; }
}

/// <summary>Write side of the tenant context; only middleware and jobs use it.</summary>
public interface ITenantSetter
{
    void Set(Guid firmId, Guid? userId, string? role);
}

public sealed class TenantNotSetException()
    : InvalidOperationException("No firm is set for this request. Data access is blocked.");
