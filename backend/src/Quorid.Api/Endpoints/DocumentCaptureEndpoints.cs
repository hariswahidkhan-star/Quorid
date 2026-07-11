using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Documents;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;
using Quorid.Infrastructure.Storage;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Module 1 — Capture &amp; Extract (spec §Module 1 / §7.2). Upload → AI classify →
/// AI extract → review → confirm. Files go to <see cref="IFileStorage"/>; the DB
/// holds only metadata and the storage key.
/// </summary>
public static class DocumentCaptureEndpoints
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".jpg", ".jpeg", ".png", ".tiff", ".tif", ".heic", ".bmp", ".xlsx", ".csv",
    };

    private const int MaxBatchFiles = 20;

    public static IEndpointRouteBuilder MapDocumentCaptureEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Capture").RequireAuthorization();

        group.MapPost("/upload", UploadAsync).DisableAntiforgery();
        group.MapPost("/upload-batch", UploadBatchAsync).DisableAntiforgery();
        group.MapPost("/{id:guid}/confirm", ConfirmAsync);
        group.MapPost("/{id:guid}/reclassify", ReclassifyAsync);
        group.MapGet("/{id:guid}/extraction", GetExtractionAsync);

        return app;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile file,
        ApplicationDbContext db,
        IFileStorage storage,
        IDocumentAiService ai,
        ICurrentUser currentUser,
        IOptions<StorageOptions> storageOptions,
        CancellationToken ct)
    {
        if (currentUser.TenantId is not { } tenantId ||
            currentUser.EntityId is not { } entityId ||
            currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        var validation = Validate(file, storageOptions.Value.MaxFileSizeBytes);
        if (validation is not null) return Results.BadRequest(new { error = validation });

        var result = await CaptureOneAsync(file, tenantId, entityId, userId, db, storage, ai, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UploadBatchAsync(
        HttpRequest request,
        ApplicationDbContext db,
        IFileStorage storage,
        IDocumentAiService ai,
        ICurrentUser currentUser,
        IOptions<StorageOptions> storageOptions,
        CancellationToken ct)
    {
        if (currentUser.TenantId is not { } tenantId ||
            currentUser.EntityId is not { } entityId ||
            currentUser.UserId is not { } userId)
        {
            return Results.Unauthorized();
        }

        if (!request.HasFormContentType)
            return Results.BadRequest(new { error = "Expected multipart/form-data." });

        var form = await request.ReadFormAsync(ct);
        var files = form.Files;

        if (files.Count == 0) return Results.BadRequest(new { error = "No files provided." });
        if (files.Count > MaxBatchFiles)
            return Results.BadRequest(new { error = $"Maximum {MaxBatchFiles} files per upload." });

        var job = new UploadJob
        {
            TenantId = tenantId,
            UserId = userId,
            EntityId = entityId,
            TotalFiles = files.Count,
            Status = UploadJobStatus.Processing,
        };
        db.UploadJobs.Add(job);
        await db.SaveChangesAsync(ct);

        var maxBytes = storageOptions.Value.MaxFileSizeBytes;
        var items = new List<BatchItemResult>();

        foreach (var file in files)
        {
            var validation = Validate(file, maxBytes);
            if (validation is not null)
            {
                items.Add(new BatchItemResult(file.FileName, false, null, validation));
                continue;
            }

            try
            {
                var result = await CaptureOneAsync(file, tenantId, entityId, userId, db, storage, ai, ct);
                items.Add(new BatchItemResult(file.FileName, true, result.DocumentId, null));
            }
            catch (Exception ex)
            {
                items.Add(new BatchItemResult(file.FileName, false, null, ex.Message));
            }
        }

        job.CompletedFiles = items.Count(i => i.Success);
        job.FailedFiles = items.Count(i => !i.Success);
        job.Status = job.FailedFiles == 0 ? UploadJobStatus.Complete : UploadJobStatus.PartialError;
        job.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new BatchUploadResultDto(
            job.Id, job.TotalFiles, job.CompletedFiles, job.FailedFiles, items));
    }

    private static async Task<IResult> ConfirmAsync(
        Guid id, ConfirmFieldsRequest request, ApplicationDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (document is null) return Results.NotFound();

        var fields = await db.ExtractedFields.Where(f => f.DocumentId == id).ToListAsync(ct);
        var byId = fields.ToDictionary(f => f.Id);

        foreach (var correction in request.Fields)
        {
            if (!byId.TryGetValue(correction.FieldId, out var field)) continue;
            if (correction.Value is not null && correction.Value != field.FieldValue)
            {
                field.OverrideValue = correction.Value;
                field.OverrideReason = correction.OverrideReason ?? "User correction";
                field.OverrideBy = currentUser.UserId;
            }
        }

        document.Status = DocumentStatus.Active;

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = document.TenantId,
            UserId = currentUser.UserId,
            Action = "DOCUMENT_CONFIRMED",
            ResourceType = nameof(Document),
            ResourceId = document.Id,
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { document.Id, Status = document.Status.ToString() });
    }

    private static async Task<IResult> ReclassifyAsync(
        Guid id, ReclassifyRequest request, ApplicationDbContext db, ICurrentUser currentUser, CancellationToken ct)
    {
        if (!DocumentDomains.IsValid(request.DomainCode))
            return Results.BadRequest(new { error = "Invalid domain code." });

        var document = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (document is null) return Results.NotFound();

        document.DomainCode = request.DomainCode;
        document.CategoryCode = request.CategoryCode;
        document.TypeCode = request.TypeCode;
        document.ClassificationMethod = ClassificationMethod.Override;
        document.ClassificationConfidence = 100m;

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = document.TenantId,
            UserId = currentUser.UserId,
            Action = "DOCUMENT_RECLASSIFIED",
            ResourceType = nameof(Document),
            ResourceId = document.Id,
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { document.Id, document.DomainCode, document.CategoryCode, document.TypeCode });
    }

    private static async Task<IResult> GetExtractionAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var exists = await db.Documents.AnyAsync(d => d.Id == id, ct);
        if (!exists) return Results.NotFound();

        var fields = await db.ExtractedFields.AsNoTracking()
            .Where(f => f.DocumentId == id)
            .Select(f => new ExtractedFieldDto(
                f.Id, f.FieldName, f.OverrideValue ?? f.FieldValue, f.Confidence, f.SourcePage))
            .ToListAsync(ct);

        return Results.Ok(fields);
    }

    // ---- helpers ----

    private static async Task<CaptureResultDto> CaptureOneAsync(
        IFormFile file, Guid tenantId, Guid entityId, Guid userId,
        ApplicationDbContext db, IFileStorage storage, IDocumentAiService ai, CancellationToken ct)
    {
        StoredFile stored;
        await using (var stream = file.OpenReadStream())
        {
            stored = await storage.SaveAsync(stream, file.FileName, file.ContentType, tenantId, ct);
        }

        var classification = await ai.ClassifyAsync(file.FileName, file.ContentType, ct);
        var fileType = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();

        var document = new Document
        {
            TenantId = tenantId,
            EntityId = entityId,
            FileName = file.FileName,
            Title = Path.GetFileNameWithoutExtension(file.FileName),
            FileType = fileType,
            FileSizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            StorageBucket = stored.Bucket,
            Version = 1,
            Status = DocumentStatus.Draft,
            DomainCode = classification.DomainCode,
            CategoryCode = classification.CategoryCode,
            TypeCode = classification.TypeCode,
            PrivacyLevel = PrivacyLevel.Controlled,
            VerificationTier = VerificationTier.T1,
            ClassificationConfidence = classification.Confidence,
            ClassificationMethod = ClassificationMethod.Ai,
            UploadedBy = userId,
        };
        db.Documents.Add(document);

        db.DocumentVersions.Add(new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = 1,
            FileName = file.FileName,
            FileSizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            IsCurrent = true,
            UploadedBy = userId,
            ExtractionStatus = "complete",
            ChangeNote = "Initial upload",
        });

        var extracted = await ai.ExtractFieldsAsync(file.FileName, classification, ct);
        var fieldEntities = extracted.Select(f => new ExtractedField
        {
            DocumentId = document.Id,
            FieldName = f.FieldName,
            FieldValue = f.FieldValue,
            Confidence = f.Confidence,
            SourcePage = f.SourcePage,
        }).ToList();
        db.ExtractedFields.AddRange(fieldEntities);

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            Action = "DOCUMENT_CAPTURED",
            ResourceType = nameof(Document),
            ResourceId = document.Id,
        });

        await db.SaveChangesAsync(ct);

        return new CaptureResultDto(
            document.Id, document.FileName, document.FileType, document.FileSizeBytes,
            document.Status.ToString(),
            new ClassificationDto(
                classification.DomainCode, classification.DomainName,
                classification.CategoryCode, classification.CategoryName,
                classification.TypeCode, classification.TypeName,
                classification.Confidence, classification.IsAutoAccepted, "Ai"),
            fieldEntities.Select(f => new ExtractedFieldDto(
                f.Id, f.FieldName, f.FieldValue, f.Confidence, f.SourcePage)).ToList());
    }

    private static string? Validate(IFormFile file, long maxBytes)
    {
        if (file.Length == 0) return "File is empty.";
        if (file.Length > maxBytes) return $"File exceeds the {maxBytes / (1024 * 1024)} MB limit.";

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            return "Unsupported file format.";

        return null;
    }
}
