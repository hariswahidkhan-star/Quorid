using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>Tracks a batch upload of up to 20 files processed in parallel.</summary>
public class UploadJob : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid UserId { get; set; }

    public Guid EntityId { get; set; }

    public Guid? FolderId { get; set; }

    public int TotalFiles { get; set; }

    public int CompletedFiles { get; set; }

    public int FailedFiles { get; set; }

    public UploadJobStatus Status { get; set; } = UploadJobStatus.Pending;

    public bool AutoClassify { get; set; } = true;

    public DateTime? CompletedAt { get; set; }
}
