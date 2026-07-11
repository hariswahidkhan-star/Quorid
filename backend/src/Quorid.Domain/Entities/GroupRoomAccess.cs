using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>Grants a whole group access to a room at a given permission level.</summary>
public class GroupRoomAccess : BaseEntity
{
    public Guid GroupId { get; set; }

    public Guid RoomId { get; set; }

    public PermissionLevel PermissionLevel { get; set; } = PermissionLevel.View;

    public Guid AssignedBy { get; set; }
}
