using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// An external guest invited to a room. Email is unique per room. The access
/// token seeds the guest's scoped JWT. NDA signature details are captured on
/// first access for audit.
/// </summary>
public class RoomGuest : BaseEntity
{
    public Guid RoomId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public RoomGuestRole Role { get; set; } = RoomGuestRole.Reviewer;

    public PermissionLevel PermissionLevel { get; set; } = PermissionLevel.View;

    public string AccessToken { get; set; } = string.Empty;

    public DateOnly? AccessExpiry { get; set; }

    public string? IpWhitelist { get; set; }

    public bool MfaRequired { get; set; }

    public DateTime? NdaSignedAt { get; set; }

    public string? NdaSignerName { get; set; }

    public string? NdaIpAddress { get; set; }

    public bool EmailVerified { get; set; }

    public DateTime? FirstAccessAt { get; set; }

    public DateTime? LastAccessAt { get; set; }

    public GuestStatus Status { get; set; } = GuestStatus.Invited;

    public Guid InvitedBy { get; set; }

    public DataRoom? Room { get; set; }
}
