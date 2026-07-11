using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Engagements;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;
using Quorid.Infrastructure.Storage;

namespace Quorid.Api.Endpoints;

/// <summary>
/// The external collection portal for vendors and clients (spec §Modules 8–9).
/// A tokenized, unauthenticated page where the party uploads documents that are
/// AI-classified, added to the host's vault, and matched to a checklist item.
/// </summary>
public static class PortalEndpoints
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".doc", ".jpg", ".jpeg", ".png", ".tiff", ".tif", ".heic", ".bmp", ".xlsx", ".csv",
    };

    public static IEndpointRouteBuilder MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/portal/{token}", GetInfoAsync).AllowAnonymous().WithTags("Portal");
        app.MapPost("/api/portal/{token}/upload", UploadAsync).AllowAnonymous().WithTags("Portal").DisableAntiforgery();
        return app;
    }

    private static async Task<IResult> GetInfoAsync(string token, ApplicationDbContext db, CancellationToken ct)
    {
        var engagement = await db.Engagements.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccessToken == token, ct);
        if (engagement is null) return Results.NotFound(new { error = "Invalid link." });
        if (engagement.Status == EngagementStatus.Archived)
            return Results.Json(new { error = "This request is closed." }, statusCode: 410);

        var host = await db.Entities.IgnoreQueryFilters()
            .Where(x => x.Id == engagement.EntityId).Select(x => x.Name).FirstOrDefaultAsync(ct) ?? string.Empty;

        var items = await db.ChecklistItems.AsNoTracking().Where(c => c.EngagementId == engagement.Id).ToListAsync(ct);
        var checklist = items
            .Select(c => new PortalChecklistDto(c.Id, c.Name, c.DocumentId != null ? "received" : "pending"))
            .ToList();

        return Results.Ok(new PortalInfoDto(engagement.Name, host, engagement.Type.ToString(), checklist));
    }

    private static async Task<IResult> UploadAsync(
        string token, HttpRequest request, ApplicationDbContext db, IFileStorage storage,
        IDocumentAiService ai, IOptions<StorageOptions> storageOptions, CancellationToken ct)
    {
        var engagement = await db.Engagements.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.AccessToken == token, ct);
        if (engagement is null) return Results.NotFound(new { error = "Invalid link." });
        if (engagement.Status == EngagementStatus.Archived)
            return Results.Json(new { error = "This request is closed." }, statusCode: 410);
        if (!request.HasFormContentType) return Results.BadRequest(new { error = "Expected multipart/form-data." });

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault();
        if (file is null || file.Length == 0) return Results.BadRequest(new { error = "A file is required." });
        if (file.Length > storageOptions.Value.MaxFileSizeBytes)
            return Results.BadRequest(new { error = "File exceeds the size limit." });

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            return Results.BadRequest(new { error = "Unsupported file format." });

        Guid? targetItemId = Guid.TryParse(form["checklistItemId"].ToString(), out var iid) ? iid : null;

        StoredFile stored;
        await using (var stream = file.OpenReadStream())
        {
            stored = await storage.SaveAsync(stream, file.FileName, file.ContentType, engagement.TenantId, ct);
        }

        var classification = await ai.ClassifyAsync(file.FileName, file.ContentType, ct);

        var doc = new Document
        {
            TenantId = engagement.TenantId,
            EntityId = engagement.EntityId,
            FileName = file.FileName,
            Title = Path.GetFileNameWithoutExtension(file.FileName),
            FileType = ext.TrimStart('.').ToLowerInvariant(),
            FileSizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            StorageBucket = stored.Bucket,
            Version = 1,
            Status = DocumentStatus.Active,
            DomainCode = classification.DomainCode,
            CategoryCode = classification.CategoryCode,
            TypeCode = classification.TypeCode,
            PrivacyLevel = PrivacyLevel.Controlled,
            VerificationTier = VerificationTier.T1,
            ClassificationConfidence = classification.Confidence,
            ClassificationMethod = ClassificationMethod.Ai,
            UploadedBy = engagement.CreatedBy,
        };
        db.Documents.Add(doc);

        db.DocumentVersions.Add(new DocumentVersion
        {
            DocumentId = doc.Id,
            VersionNumber = 1,
            FileName = file.FileName,
            FileSizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            IsCurrent = true,
            UploadedBy = engagement.CreatedBy,
            ExtractionStatus = "complete",
            ChangeNote = "Uploaded via collection portal",
        });

        foreach (var f in await ai.ExtractFieldsAsync(file.FileName, classification, ct))
        {
            db.ExtractedFields.Add(new ExtractedField
            {
                DocumentId = doc.Id, FieldName = f.FieldName, FieldValue = f.FieldValue,
                Confidence = f.Confidence, SourcePage = f.SourcePage,
            });
        }

        var pending = await db.ChecklistItems.IgnoreQueryFilters()
            .Where(c => c.EngagementId == engagement.Id && c.DocumentId == null).ToListAsync(ct);
        var item = targetItemId is { } tid ? pending.FirstOrDefault(c => c.Id == tid) : null;
        item ??= pending.FirstOrDefault(c => c.DomainCode != null && c.DomainCode == classification.DomainCode)
                 ?? pending.FirstOrDefault();
        if (item is not null)
        {
            item.DocumentId = doc.Id;
            item.ReceivedAt = DateTime.UtcNow;
        }

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = engagement.TenantId,
            UserId = null,
            Action = "PORTAL_UPLOAD",
            ResourceType = "Engagement",
            ResourceId = engagement.Id,
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { uploaded = true, documentId = doc.Id, matchedItem = item?.Id });
    }
}
