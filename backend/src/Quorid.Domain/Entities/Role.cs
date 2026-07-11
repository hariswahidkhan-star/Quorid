using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>
/// RBAC role. Permissions are stored as a JSON object of boolean flags
/// (spec §Screen 9 Role Management). System roles (Owner, Admin, Viewer)
/// cannot be deleted.
/// </summary>
public class Role : BaseEntity, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    /// <summary>Permission flags as a JSON object, e.g. {"viewDocuments":true,...}.</summary>
    public string Permissions { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; }
}
