using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>
/// A compliance framework (spec §Module 6). System frameworks (OSHA, SOC 2, …)
/// ship pre-built; admins can also create custom frameworks.
/// </summary>
public class ComplianceFramework : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public string Status { get; set; } = "active";
}
