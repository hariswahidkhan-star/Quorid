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
/// Module 3 — Document Management (spec §Module 3 / §7.2). Metadata editing,
/// soft delete, version history + restore, tags, activity trail, and expiry
/// tracking.
/// </summary>
public static class DocumentManagementEndpoints
{
    public static IEndpointRouteBuilder MapDocumentManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Documents").RequireAuthorization();

        group.MapGet("/expiring", ExpiringAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", ArchiveAsync);
        group.MapGet("/{id:guid}/versions", ListVersionsAsync);
        group.MapPost("/{id:guid}/versions", AddVersionAsync).DisableAntiforgery();
        group.MapPost("/{id:guid}/versions/{versionId:guid}/restore", RestoreVersionAsync);
        group.MapPost("/{id:guid}/tags", AddTagsAsync);
        group.MapDelete("/{id:guid}/tags/{tagName}", RemoveTagAsync);
        group.MapGet("/{id:guid}/activity", ActivityAsync);

        return app;
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateDocumentRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Results.NotFound();

        if (request.Title is not null) doc.Title = request.Title;
        if (request.Description is not null) doc.Description = request.Description;
        if (request.DomainCode is not null) doc.DomainCode = request.DomainCode;
        if (request.CategoryCode is not null) doc.CategoryCode = request.CategoryCode;
        if (request.TypeCode is not null) doc.TypeCode = request.TypeCode;
        if (request.SubtypeCode is not null) doc.SubtypeCode = request.SubtypeCode;
        if (request.ExpiryDate is not null) doc.ExpiryDate = request.ExpiryDate;
        if (request.PrivacyLevel is not null &&
            Enum.TryParse<PrivacyLevel>(request.PrivacyLevel, ignoreCase: true, out var privacy))
        {
            doc.PrivacyLevel = privacy;
        }

        if (request.Tags is not null)
        {
            var existing = await db.DocumentTags.Where(t => t.DocumentId == id).ToListAsync(ct);
            db.DocumentTags.RemoveRange(existing);
            foreach (var tag in request.Tags
                         .Where(t => !string.IsNullOrWhiteSpace(t))
                         .Select(t => t.Trim())
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                db.DocumentTags.Add(new DocumentTag { DocumentId = id, TagName = tag, Source = TagSource.Manual });
            }
        }

        db.AuditLogs.Add(Audit(doc.TenantId, user.UserId, "DOCUMENT_UPDATED", doc.Id));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { doc.Id, doc.Title, Status = doc.Status.ToString() });
    }

    private static async Task<IResult> ArchiveAsync(
        Guid id, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Results.NotFound();

        doc.Status = DocumentStatus.Archived;

        // Soft delete also removes the document from any rooms that reference it.
        var roomRefs = await db.RoomDocuments.Where(r => r.DocumentId == id).ToListAsync(ct);
        db.RoomDocuments.RemoveRange(roomRefs);

        db.AuditLogs.Add(Audit(doc.TenantId, user.UserId, "DOCUMENT_ARCHIVED", doc.Id));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { doc.Id, Status = doc.Status.ToString() });
    }

    private static async Task<IResult> ListVersionsAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.Documents.AnyAsync(d => d.Id == id, ct)) return Results.NotFound();

        var versions = await db.DocumentVersions.AsNoTracking()
            .Where(v => v.DocumentId == id)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionDto(
                v.Id, v.VersionNumber, v.FileName, v.FileSizeBytes, v.ChangeNote,
                v.AiDiffSummary, v.IsCurrent, v.UploadedBy, v.CreatedAt))
            .ToListAsync(ct);

        return Results.Ok(versions);
    }

    private static async Task<IResult> AddVersionAsync(
        Guid id, HttpRequest request, ApplicationDbContext db, IFileStorage storage,
        ICurrentUser user, IOptions<StorageOptions> storageOptions, CancellationToken ct)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Results.NotFound();
        if (!request.HasFormContentType) return Results.BadRequest(new { error = "Expected multipart/form-data." });

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault();
        if (file is null || file.Length == 0) return Results.BadRequest(new { error = "A file is required." });
        if (file.Length > storageOptions.Value.MaxFileSizeBytes)
            return Results.BadRequest(new { error = "File exceeds the size limit." });

        var changeNote = form["changeNote"].ToString();
        var userId = user.UserId ?? Guid.Empty;

        StoredFile stored;
        await using (var stream = file.OpenReadStream())
        {
            stored = await storage.SaveAsync(stream, file.FileName, file.ContentType, doc.TenantId, ct);
        }

        foreach (var current in await db.DocumentVersions.Where(v => v.DocumentId == id && v.IsCurrent).ToListAsync(ct))
            current.IsCurrent = false;

        var newVersionNumber = doc.Version + 1;
        var version = new DocumentVersion
        {
            DocumentId = id,
            VersionNumber = newVersionNumber,
            FileName = file.FileName,
            FileSizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            IsCurrent = true,
            ChangeNote = string.IsNullOrWhiteSpace(changeNote) ? null : changeNote,
            AiDiffSummary = "New version uploaded.",
            UploadedBy = userId,
            ExtractionStatus = "skipped",
        };
        db.DocumentVersions.Add(version);

        doc.FileName = file.FileName;
        doc.FileSizeBytes = stored.SizeBytes;
        doc.StorageKey = stored.StorageKey;
        doc.StorageBucket = stored.Bucket;
        doc.FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        doc.Version = newVersionNumber;

        db.AuditLogs.Add(Audit(doc.TenantId, user.UserId, "DOCUMENT_VERSION_ADDED", doc.Id));
        await db.SaveChangesAsync(ct);

        return Results.Ok(new DocumentVersionDto(
            version.Id, version.VersionNumber, version.FileName, version.FileSizeBytes,
            version.ChangeNote, version.AiDiffSummary, version.IsCurrent, version.UploadedBy, version.CreatedAt));
    }

    private static async Task<IResult> RestoreVersionAsync(
        Guid id, Guid versionId, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Results.NotFound();

        var target = await db.DocumentVersions.FirstOrDefaultAsync(v => v.Id == versionId && v.DocumentId == id, ct);
        if (target is null) return Results.NotFound();

        foreach (var current in await db.DocumentVersions.Where(v => v.DocumentId == id && v.IsCurrent).ToListAsync(ct))
            current.IsCurrent = false;

        var newVersionNumber = doc.Version + 1;
        var restored = new DocumentVersion
        {
            DocumentId = id,
            VersionNumber = newVersionNumber,
            FileName = target.FileName,
            FileSizeBytes = target.FileSizeBytes,
            StorageKey = target.StorageKey,
            IsCurrent = true,
            ChangeNote = $"Restored from v{target.VersionNumber}",
            AiDiffSummary = $"Reverted to version {target.VersionNumber}.",
            UploadedBy = user.UserId ?? Guid.Empty,
        };
        db.DocumentVersions.Add(restored);

        doc.FileName = target.FileName;
        doc.FileSizeBytes = target.FileSizeBytes;
        doc.StorageKey = target.StorageKey;
        doc.Version = newVersionNumber;

        db.AuditLogs.Add(Audit(doc.TenantId, user.UserId, "DOCUMENT_VERSION_RESTORED", doc.Id));
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { restored.Id, restored.VersionNumber });
    }

    private static async Task<IResult> AddTagsAsync(
        Guid id, AddTagsRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Results.NotFound();

        var existing = await db.DocumentTags.Where(t => t.DocumentId == id).Select(t => t.TagName).ToListAsync(ct);
        var seen = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        foreach (var tag in request.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()))
        {
            if (seen.Add(tag))
                db.DocumentTags.Add(new DocumentTag { DocumentId = id, TagName = tag, Source = TagSource.Manual });
        }

        db.AuditLogs.Add(Audit(doc.TenantId, user.UserId, "DOCUMENT_TAGGED", doc.Id));
        await db.SaveChangesAsync(ct);
        return Results.Ok(seen.ToList());
    }

    private static async Task<IResult> RemoveTagAsync(
        Guid id, string tagName, ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.Documents.AnyAsync(d => d.Id == id, ct)) return Results.NotFound();

        var tags = await db.DocumentTags.Where(t => t.DocumentId == id && t.TagName == tagName).ToListAsync(ct);
        db.DocumentTags.RemoveRange(tags);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ActivityAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.Documents.AnyAsync(d => d.Id == id, ct)) return Results.NotFound();

        var entries = await db.AuditLogs.AsNoTracking()
            .Where(a => a.ResourceId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .Select(a => new ActivityEntryDto(a.Action, a.UserId, a.CreatedAt, a.Details))
            .ToListAsync(ct);

        return Results.Ok(entries);
    }

    private static async Task<IResult> ExpiringAsync(ApplicationDbContext db, int? days, CancellationToken ct)
    {
        var window = days is > 0 ? days.Value : 90;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(window);

        var docs = await db.Documents.AsNoTracking()
            .Where(d => d.Status != DocumentStatus.Archived && d.ExpiryDate != null && d.ExpiryDate <= horizon)
            .OrderBy(d => d.ExpiryDate)
            .Select(d => new { d.Id, d.FileName, d.Title, d.DomainCode, d.ExpiryDate })
            .ToListAsync(ct);

        var result = docs
            .Where(d => d.ExpiryDate is not null)
            .Select(d => new ExpiringDocumentDto(
                d.Id, d.FileName, d.Title, d.DomainCode,
                d.ExpiryDate!.Value, d.ExpiryDate!.Value.DayNumber - today.DayNumber))
            .ToList();

        return Results.Ok(result);
    }

    private static AuditLog Audit(Guid tenantId, Guid? userId, string action, Guid resourceId) =>
        new()
        {
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            ResourceType = nameof(Document),
            ResourceId = resourceId,
        };
}
