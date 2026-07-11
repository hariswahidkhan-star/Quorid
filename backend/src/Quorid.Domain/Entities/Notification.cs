using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>An in-app notification for a user.</summary>
public class Notification : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Body { get; set; }

    public string? ResourceType { get; set; }

    public Guid? ResourceId { get; set; }

    public bool Read { get; set; }
}
