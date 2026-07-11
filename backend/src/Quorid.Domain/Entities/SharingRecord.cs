using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// An external share of a single vault document (spec §Module 5 / §6.5
/// sharing_records). The access token gates a tokenized, unauthenticated view;
/// the 8-level <see cref="PermissionLevel"/> controls what the recipient can do.
/// </summary>
public class SharingRecord : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid DocumentId { get; set; }

    public string RecipientEmail { get; set; } = string.Empty;

    public string? RecipientName { get; set; }

    public PermissionLevel PermissionLevel { get; set; } = PermissionLevel.View;

    public DateOnly? ExpiryDate { get; set; }

    public bool Watermark { get; set; } = true;

    public bool TrackViews { get; set; } = true;

    /// <summary>Unique, cryptographically random token embedded in the share link.</summary>
    public string AccessToken { get; set; } = string.Empty;

    public SharingStatus Status { get; set; } = SharingStatus.Active;

    public string? Message { get; set; }

    public int ViewCount { get; set; }

    public DateTime? LastViewedAt { get; set; }

    public Guid CreatedBy { get; set; }
}
