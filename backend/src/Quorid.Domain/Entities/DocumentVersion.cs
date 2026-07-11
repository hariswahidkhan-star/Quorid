using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>
/// One immutable version of a document's file. Restoring an old version creates
/// a new version rather than mutating history.
/// </summary>
public class DocumentVersion : BaseEntity
{
    public Guid DocumentId { get; set; }

    public int VersionNumber { get; set; }

    public string FileName { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string StorageKey { get; set; } = string.Empty;

    public string? ChangeNote { get; set; }

    /// <summary>AI-generated human-readable diff between versions.</summary>
    public string? AiDiffSummary { get; set; }

    public bool IsCurrent { get; set; }

    public Guid UploadedBy { get; set; }

    /// <summary>pending, processing, complete, skipped.</summary>
    public string? ExtractionStatus { get; set; }

    public Document? Document { get; set; }
}
