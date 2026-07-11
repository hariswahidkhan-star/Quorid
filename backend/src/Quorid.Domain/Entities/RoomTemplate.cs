using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>
/// Default folder structure and suggested document types for a room type,
/// stored as JSON.
/// </summary>
public class RoomTemplate : BaseEntity
{
    public Guid RoomTypeId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Ordered folder definitions as a JSON array.</summary>
    public string FolderStructure { get; set; } = "[]";

    /// <summary>Suggested document type codes as a JSON array.</summary>
    public string SuggestedDocTypes { get; set; } = "[]";
}
