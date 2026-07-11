namespace Quorid.Application.Documents;

/// <summary>Editable document metadata (spec §Screen 6 Metadata tab).</summary>
public record UpdateDocumentRequest(
    string? Title,
    string? Description,
    string? DomainCode,
    string? CategoryCode,
    string? TypeCode,
    string? SubtypeCode,
    string? PrivacyLevel,
    DateOnly? ExpiryDate,
    IReadOnlyList<string>? Tags);

public record AddTagsRequest(IReadOnlyList<string> Tags);

public record DocumentVersionDto(
    Guid Id,
    int VersionNumber,
    string FileName,
    long FileSizeBytes,
    string? ChangeNote,
    string? AiDiffSummary,
    bool IsCurrent,
    Guid UploadedBy,
    DateTime CreatedAt);

public record ActivityEntryDto(string Action, Guid? UserId, DateTime CreatedAt, string? Details);

public record ExpiringDocumentDto(
    Guid Id,
    string FileName,
    string? Title,
    string? DomainCode,
    DateOnly ExpiryDate,
    int DaysUntilExpiry);
