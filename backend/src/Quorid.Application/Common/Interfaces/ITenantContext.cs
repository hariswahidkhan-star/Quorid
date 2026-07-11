namespace Quorid.Application.Common.Interfaces;

/// <summary>
/// Ambient tenant for the current request, resolved from the JWT by middleware
/// and consumed by the persistence layer's global query filter.
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }

    bool HasTenant { get; }

    void SetTenant(Guid tenantId);
}
