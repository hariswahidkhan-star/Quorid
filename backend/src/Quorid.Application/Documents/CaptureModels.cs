namespace Quorid.Application.Documents;

/// <summary>Classification as returned to the capture-review UI.</summary>
public record ClassificationDto(
    string DomainCode,
    string DomainName,
    string? CategoryCode,
    string? CategoryName,
    string? TypeCode,
    string? TypeName,
    decimal Confidence,
    bool AutoAccepted,
    string Method);

public record ExtractedFieldDto(
    Guid Id,
    string FieldName,
    string? FieldValue,
    decimal? Confidence,
    int? SourcePage);

/// <summary>Result of capturing a single document (upload → classify → extract).</summary>
public record CaptureResultDto(
    Guid DocumentId,
    string FileName,
    string FileType,
    long FileSizeBytes,
    string Status,
    ClassificationDto Classification,
    IReadOnlyList<ExtractedFieldDto> Fields);

public record FieldCorrection(Guid FieldId, string? Value, string? OverrideReason);

public record ConfirmFieldsRequest(IReadOnlyList<FieldCorrection> Fields);

public record ReclassifyRequest(string DomainCode, string? CategoryCode, string? TypeCode);

public record BatchItemResult(string FileName, bool Success, Guid? DocumentId, string? Error);

public record BatchUploadResultDto(
    Guid BatchId,
    int Total,
    int Completed,
    int Failed,
    IReadOnlyList<BatchItemResult> Items);
