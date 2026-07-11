using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// A page-level view event by a guest, powering engagement analytics and
/// heatmaps. Screenshot attempts are logged here too.
/// </summary>
public class DocumentView : BaseEntity
{
    public Guid GuestId { get; set; }

    public Guid DocumentId { get; set; }

    public Guid RoomId { get; set; }

    public int? PageNumber { get; set; }

    public int DurationSeconds { get; set; }

    public DateTime ViewedAt { get; set; }

    public DocumentViewAction Action { get; set; } = DocumentViewAction.View;
}
