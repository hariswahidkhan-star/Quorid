using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// A collection of users for bulk access control. Name is unique per entity.
/// </summary>
public class Group : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid EntityId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public GroupStatus Status { get; set; } = GroupStatus.Active;

    public Guid CreatedBy { get; set; }

    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
}
