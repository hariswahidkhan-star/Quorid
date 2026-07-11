using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>A single access event on a shared document, for view analytics.</summary>
public class ShareView : BaseEntity
{
    public Guid SharingRecordId { get; set; }

    public string IpAddress { get; set; } = string.Empty;

    public string? UserAgent { get; set; }

    public DateTime ViewedAt { get; set; }

    public int DurationSeconds { get; set; }
}
