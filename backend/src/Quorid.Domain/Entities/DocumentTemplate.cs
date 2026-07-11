using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>
/// A reusable document blueprint for the creation studio (Module 4). The
/// <see cref="Body"/> holds text with <c>{{token}}</c> placeholders that the
/// generator fills from the entity profile and user-supplied variables. System
/// templates are seeded; users may add their own.
/// </summary>
public class DocumentTemplate : BaseEntity, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Stable machine key (e.g. "nda", "engagement-letter"), unique per tenant.</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Template body with {{token}} placeholders.</summary>
    public string Body { get; set; } = string.Empty;

    public bool IsSystem { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }
}
