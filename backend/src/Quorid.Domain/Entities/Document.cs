using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// Core document record. The file itself lives in S3 (see <see cref="StorageKey"/>);
/// the database holds only metadata. A document exists once in the vault and is
/// referenced into rooms — never copied.
/// </summary>
public class Document : BaseEntity, ITenantScoped, IAuditableEntity, ISoftDeletable
{
    public Guid TenantId { get; set; }

    public Guid EntityId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string? Title { get; set; }

    public string? Description { get; set; }

    /// <summary>pdf, docx, xlsx, jpg, png, tiff, ...</summary>
    public string FileType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    /// <summary>S3 object key. Never store file bytes in the database.</summary>
    public string StorageKey { get; set; } = string.Empty;

    public string StorageBucket { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public string? DomainCode { get; set; }
    public string? CategoryCode { get; set; }
    public string? TypeCode { get; set; }
    public string? SubtypeCode { get; set; }

    public PrivacyLevel PrivacyLevel { get; set; } = PrivacyLevel.Controlled;

    public VerificationTier VerificationTier { get; set; } = VerificationTier.T1;

    public DateOnly? ExpiryDate { get; set; }

    /// <summary>AI classification confidence, 0–100.</summary>
    public decimal? ClassificationConfidence { get; set; }

    public ClassificationMethod? ClassificationMethod { get; set; }

    public Guid UploadedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool IsDeleted => Status == DocumentStatus.Archived;

    public ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public ICollection<ExtractedField> ExtractedFields { get; set; } = new List<ExtractedField>();
    public ICollection<DocumentTag> Tags { get; set; } = new List<DocumentTag>();
}
