using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>
/// Organizational department, optionally nested via <see cref="ParentId"/>
/// (spec §8.1 Organization Hierarchy).
/// </summary>
public class Department : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid? ParentId { get; set; }

    public int DisplayOrder { get; set; }
}
