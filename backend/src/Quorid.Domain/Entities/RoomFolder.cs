using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>A folder within a data room. May nest via <see cref="ParentFolderId"/>.</summary>
public class RoomFolder : BaseEntity
{
    public Guid RoomId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public Guid? ParentFolderId { get; set; }

    /// <summary>True when user-created rather than from the template.</summary>
    public bool IsCustom { get; set; }

    public DataRoom? Room { get; set; }
}
