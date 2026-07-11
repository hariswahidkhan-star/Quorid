using Quorid.Application.Common.Interfaces;

namespace Quorid.Infrastructure.Multitenancy;

/// <summary>Request-scoped tenant, set by middleware from the JWT's tenant_id claim.</summary>
public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public bool HasTenant => TenantId.HasValue;

    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}
