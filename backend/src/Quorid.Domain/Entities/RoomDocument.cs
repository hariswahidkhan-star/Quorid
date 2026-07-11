using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// A reference to a vault document placed in a room folder. Unique on
/// (RoomId, DocumentId). An optional permission override tightens or loosens
/// the room-level default for this document.
/// </summary>
public class RoomDocument : BaseEntity
{
    public Guid RoomId { get; set; }

    public Guid FolderId { get; set; }

    public Guid DocumentId { get; set; }

    public Guid AddedBy { get; set; }

    public PermissionLevel? PermissionOverride { get; set; }

    public DataRoom? Room { get; set; }
}
