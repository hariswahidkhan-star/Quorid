using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// A secure, time-limited document-sharing environment for external
/// stakeholders. Name is unique per (tenant, entity). Documents are referenced
/// in, not copied (see <see cref="RoomDocument"/>).
/// </summary>
public class DataRoom : BaseEntity, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }

    public Guid EntityId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Guid RoomTypeId { get; set; }

    public Guid? TemplateId { get; set; }

    public RoomStatus Status { get; set; } = RoomStatus.Draft;

    public DateOnly ExpiryDate { get; set; }

    public bool NdaRequired { get; set; } = true;

    public Guid? NdaDocumentId { get; set; }

    public bool WatermarkEnabled { get; set; } = true;

    public string? WatermarkText { get; set; } = "CONFIDENTIAL";

    public bool QaEnabled { get; set; } = true;

    /// <summary>bcrypt hash of the optional room password.</summary>
    public string? PasswordHash { get; set; }

    public DownloadPolicy DownloadPolicy { get; set; } = DownloadPolicy.ViewOnly;

    public bool AnalyticsEnabled { get; set; } = true;

    public AccessHours AccessHours { get; set; } = AccessHours.TwentyFourSeven;

    public TimeOnly? AccessHoursStart { get; set; }

    public TimeOnly? AccessHoursEnd { get; set; }

    public string? WelcomeMessage { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<RoomFolder> Folders { get; set; } = new List<RoomFolder>();
    public ICollection<RoomDocument> Documents { get; set; } = new List<RoomDocument>();
    public ICollection<RoomGuest> Guests { get; set; } = new List<RoomGuest>();
}
