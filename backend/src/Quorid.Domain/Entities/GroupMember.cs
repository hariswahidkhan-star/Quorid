using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>
/// Join row linking a user to a group. Unique on (GroupId, UserId).
/// <see cref="BaseEntity.CreatedAt"/> records when the user was added.
/// </summary>
public class GroupMember : BaseEntity
{
    public Guid GroupId { get; set; }

    public Guid UserId { get; set; }

    public Guid AddedBy { get; set; }

    public Group? Group { get; set; }
}
