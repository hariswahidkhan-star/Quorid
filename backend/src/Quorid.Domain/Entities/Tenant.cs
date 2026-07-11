using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// Top-level isolation boundary. Every other tenant-scoped row references a tenant.
/// </summary>
public class Tenant : BaseEntity, IAuditableEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Primary email/subdomain, e.g. "apex" in apex.quorid.com.</summary>
    public string? Domain { get; set; }

    public PlanTier Plan { get; set; } = PlanTier.Starter;

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    public DateTime UpdatedAt { get; set; }
}
