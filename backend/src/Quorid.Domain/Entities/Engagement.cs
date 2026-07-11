using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// A project, vendor, or client engagement (Modules 7–9). Each carries a
/// document checklist and, for vendors/clients, an external upload portal token.
/// </summary>
public class Engagement : BaseEntity, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }

    public Guid EntityId { get; set; }

    public EngagementType Type { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Contact for collection requests / external portal invites (vendors, clients).</summary>
    public string? ContactEmail { get; set; }

    public EngagementStatus Status { get; set; } = EngagementStatus.Active;

    public DateOnly? DueDate { get; set; }

    /// <summary>Token for the external upload portal; null for internal projects.</summary>
    public string? AccessToken { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<ChecklistItem> Checklist { get; set; } = new List<ChecklistItem>();
}
