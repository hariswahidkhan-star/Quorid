using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>
/// A required document within an engagement's collection checklist. It is
/// satisfied when <see cref="DocumentId"/> is set (received).
/// </summary>
public class ChecklistItem : BaseEntity
{
    public Guid EngagementId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Optional document domain that satisfies this item (e.g. "INS").</summary>
    public string? DomainCode { get; set; }

    public bool IsRequired { get; set; } = true;

    public Guid? DocumentId { get; set; }

    public DateTime? ReceivedAt { get; set; }

    public Engagement? Engagement { get; set; }
}
