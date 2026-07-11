using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// The 4-level document classification tree (Domain &gt; Category &gt; Type &gt; Sub-Type).
/// </summary>
public class DocumentTaxonomy : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string DomainCode { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;

    public string? CategoryCode { get; set; }
    public string? CategoryName { get; set; }

    public string? TypeCode { get; set; }
    public string? TypeName { get; set; }

    public string? SubtypeCode { get; set; }
    public string? SubtypeName { get; set; }

    public bool HasExpiry { get; set; }

    /// <summary>AI generation category: A-Safe, B-Supervised, C-Blocked.</summary>
    public string? AiGenCategory { get; set; }

    public PrivacyLevel DefaultPrivacy { get; set; } = PrivacyLevel.Controlled;

    /// <summary>Retention period in days.</summary>
    public int? RetentionPeriod { get; set; }

    public int DisplayOrder { get; set; }

    public string Status { get; set; } = "active";
}
