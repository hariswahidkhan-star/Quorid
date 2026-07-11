using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// A guest question in a room's Q&amp;A module. AI drafts an answer with citations;
/// an internal expert reviews and publishes it.
/// </summary>
public class QaQuestion : BaseEntity
{
    public Guid RoomId { get; set; }

    public Guid GuestId { get; set; }

    public Guid? DocumentId { get; set; }

    public QaCategory Category { get; set; } = QaCategory.General;

    public string QuestionText { get; set; } = string.Empty;

    public QaStatus Status { get; set; } = QaStatus.Pending;

    public Guid? AssignedTo { get; set; }

    public string? AnswerText { get; set; }

    public Guid? AnsweredBy { get; set; }

    public DateTime? AnsweredAt { get; set; }

    /// <summary>When true, the answer is visible to all guests, not just the asker.</summary>
    public bool IsPublic { get; set; }
}
