using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// An internal platform user. Email is unique per tenant. Passwords are stored
/// as bcrypt hashes; MFA secrets are encrypted at the application layer.
/// </summary>
public class User : BaseEntity, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }

    public Guid EntityId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public Guid RoleId { get; set; }

    public Guid? DepartmentId { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Invited;

    public string? PasswordHash { get; set; }

    public string? MfaSecret { get; set; }

    public bool MfaEnabled { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public string? LastLoginIp { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTime? LockedUntil { get; set; }

    public string? AvatarUrl { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Role? Role { get; set; }
}
