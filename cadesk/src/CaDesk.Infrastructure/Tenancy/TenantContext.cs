using CaDesk.Application.Abstractions;

namespace CaDesk.Infrastructure.Tenancy;

/// <summary>Scoped per request / job. Can be set once; changing firm mid-scope is refused.</summary>
public sealed class TenantContext : ITenantContext, ITenantSetter
{
    private Guid? _firmId;

    public bool HasTenant => _firmId.HasValue;
    public Guid FirmId => _firmId ?? throw new TenantNotSetException();
    public Guid? UserId { get; private set; }
    public string? Role { get; private set; }

    public void Set(Guid firmId, Guid? userId, string? role)
    {
        if (firmId == Guid.Empty) throw new ArgumentException("Firm id is empty.", nameof(firmId));
        if (_firmId.HasValue && _firmId.Value != firmId)
            throw new InvalidOperationException("The firm for this scope is already set and cannot change.");
        _firmId = firmId;
        UserId = userId;
        Role = role;
    }
}
