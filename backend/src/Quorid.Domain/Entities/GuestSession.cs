using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>A single guest browsing session, for analytics and audit.</summary>
public class GuestSession : BaseEntity
{
    public Guid GuestId { get; set; }

    public Guid RoomId { get; set; }

    public string IpAddress { get; set; } = string.Empty;

    public string UserAgent { get; set; } = string.Empty;

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public int PagesViewed { get; set; }
}
