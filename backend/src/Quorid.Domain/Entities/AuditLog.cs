using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>
/// Immutable, append-only record of every state change. Never updated or
/// deleted. Never stores sensitive data (passwords, MFA secrets, file contents) —
/// actions and metadata only.
/// </summary>
public class AuditLog : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public Guid? ResourceId { get; set; }

    /// <summary>Optional room reference, powering room "last activity" queries.</summary>
    public Guid? RoomId { get; set; }

    /// <summary>Non-sensitive contextual metadata as JSON.</summary>
    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }
}
