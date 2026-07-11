using System.Text;
using Microsoft.EntityFrameworkCore;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.DocumentCreation;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Module 4 — Document Creation studio (spec §Module 4). A template library, an
/// offline AI composer that drafts from a prompt, an editable draft workspace with
/// an "improve" pass, and finalize-to-vault promotion.
/// </summary>
public static class DocumentCreationEndpoints
{
    public static IEndpointRouteBuilder MapDocumentStudioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/studio").WithTags("Document Studio").RequireAuthorization();

        group.MapGet("/templates", ListTemplatesAsync);
        group.MapPost("/templates", CreateTemplateAsync);
        group.MapGet("/drafts", ListDraftsAsync);
        group.MapPost("/drafts", GenerateAsync);
        group.MapGet("/drafts/{id:guid}", DraftDetailAsync);
        group.MapPut("/drafts/{id:guid}", UpdateDraftAsync);
        group.MapDelete("/drafts/{id:guid}", DeleteDraftAsync);
        group.MapPost("/drafts/{id:guid}/improve", ImproveDraftAsync);
        group.MapPost("/drafts/{id:guid}/finalize", FinalizeDraftAsync);

        return app;
    }

    // ---- Templates ----

    private static async Task<IResult> ListTemplatesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var templates = await EnsureTemplatesSeededAsync(db, ct);
        var list = templates
            .OrderBy(t => t.Category).ThenBy(t => t.Name)
            .Select(t => new TemplateDto(t.Key, t.Name, t.Category, t.Description, t.IsSystem))
            .ToList();
        return Results.Ok(list);
    }

    private static async Task<IResult> CreateTemplateAsync(
        CreateTemplateRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Body))
            return Results.BadRequest(new { error = "Name and body are required." });
        if (user.TenantId is null)
            return Results.Unauthorized();

        await EnsureTemplatesSeededAsync(db, ct);
        var key = await UniqueKeyAsync(db, Slug(request.Name), ct);

        var template = new DocumentTemplate
        {
            Key = key,
            Name = request.Name.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Custom" : request.Category.Trim(),
            Description = request.Description,
            Body = request.Body,
            IsSystem = false,
            CreatedBy = user.UserId ?? Guid.Empty,
        };
        db.DocumentTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new TemplateDto(template.Key, template.Name, template.Category, template.Description, false));
    }

    // ---- Drafts ----

    private static async Task<IResult> ListDraftsAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var rows = await db.DocumentDrafts.AsNoTracking()
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new { d.Id, d.Title, d.TemplateKey, d.Status, d.DocumentId, d.UpdatedAt })
            .ToListAsync(ct);

        var list = rows.Select(d => new DraftListItemDto(
            d.Id, d.Title, d.TemplateKey, d.Status.ToString(), d.DocumentId != null, d.UpdatedAt)).ToList();
        return Results.Ok(list);
    }

    private static async Task<IResult> GenerateAsync(
        GenerateDraftRequest request, ApplicationDbContext db, ICurrentUser user,
        IDocumentGenerator generator, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Prompt) && string.IsNullOrWhiteSpace(request.TemplateKey))
            return Results.BadRequest(new { error = "Provide a prompt or a template." });
        if (user.TenantId is not { } tenantId || user.UserId is not { } userId)
            return Results.Unauthorized();

        var entityId = user.EntityId ?? Guid.Empty;
        string? templateBody = null;
        string? templateName = null;

        if (!string.IsNullOrWhiteSpace(request.TemplateKey))
        {
            await EnsureTemplatesSeededAsync(db, ct);
            var template = await db.DocumentTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Key == request.TemplateKey, ct);
            templateBody = template?.Body;
            templateName = template?.Name;
        }

        var tokens = await BuildTokensAsync(db, entityId, request, ct);
        var docType = string.IsNullOrWhiteSpace(request.DocumentType)
            ? templateName ?? "Document"
            : request.DocumentType;

        var body = await generator.GenerateAsync(
            new GenerationRequest(docType, templateBody, request.Prompt ?? string.Empty, tokens), ct);

        var draft = new DocumentDraft
        {
            TenantId = tenantId,
            EntityId = entityId,
            Title = string.IsNullOrWhiteSpace(request.Title) ? (templateName ?? docType) : request.Title.Trim(),
            TemplateKey = request.TemplateKey,
            Prompt = request.Prompt,
            Body = body,
            Status = DraftStatus.Draft,
            CreatedBy = userId,
        };
        db.DocumentDrafts.Add(draft);
        db.AuditLogs.Add(Audit(tenantId, userId, "DRAFT_GENERATED", draft.Id));
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToDetail(draft));
    }

    private static async Task<IResult> DraftDetailAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var draft = await db.DocumentDrafts.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        return draft is null ? Results.NotFound() : Results.Ok(ToDetail(draft));
    }

    private static async Task<IResult> UpdateDraftAsync(
        Guid id, UpdateDraftRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var draft = await db.DocumentDrafts.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (draft is null) return Results.NotFound();
        if (draft.Status == DraftStatus.Finalized)
            return Results.BadRequest(new { error = "A finalized draft cannot be edited." });

        if (request.Title is not null) draft.Title = request.Title;
        if (request.Body is not null) draft.Body = request.Body;
        if (request.Status is not null && Enum.TryParse<DraftStatus>(request.Status, ignoreCase: true, out var status)
            && status != DraftStatus.Finalized)
            draft.Status = status;

        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDetail(draft));
    }

    private static async Task<IResult> DeleteDraftAsync(
        Guid id, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var draft = await db.DocumentDrafts.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (draft is null) return Results.NotFound();

        db.AuditLogs.Add(Audit(draft.TenantId, user.UserId, "DRAFT_DELETED", id));
        db.DocumentDrafts.Remove(draft);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ImproveDraftAsync(
        Guid id, ImproveRequest request, ApplicationDbContext db, IDocumentGenerator generator, CancellationToken ct)
    {
        var draft = await db.DocumentDrafts.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (draft is null) return Results.NotFound();
        if (draft.Status == DraftStatus.Finalized)
            return Results.BadRequest(new { error = "A finalized draft cannot be edited." });

        draft.Body = await generator.ImproveAsync(draft.Body, request.Instruction ?? "polish", ct);
        await db.SaveChangesAsync(ct);
        return Results.Ok(ToDetail(draft));
    }

    private static async Task<IResult> FinalizeDraftAsync(
        Guid id, ApplicationDbContext db, ICurrentUser user, IFileStorage storage, CancellationToken ct)
    {
        var draft = await db.DocumentDrafts.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (draft is null) return Results.NotFound();
        if (draft.Status == DraftStatus.Finalized)
            return Results.BadRequest(new { error = "This draft is already finalized." });
        if (user.TenantId is not { } tenantId || user.UserId is not { } userId)
            return Results.Unauthorized();

        var fileName = Slug(draft.Title) + ".txt";
        var bytes = Encoding.UTF8.GetBytes(draft.Body);
        StoredFile stored;
        await using (var stream = new MemoryStream(bytes))
        {
            stored = await storage.SaveAsync(stream, fileName, "text/plain", tenantId, ct);
        }

        var document = new Document
        {
            TenantId = tenantId,
            EntityId = draft.EntityId,
            FileName = fileName,
            Title = draft.Title,
            FileType = "txt",
            FileSizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            StorageBucket = stored.Bucket,
            Version = 1,
            Status = DocumentStatus.Draft,
            PrivacyLevel = PrivacyLevel.Controlled,
            VerificationTier = VerificationTier.T1,
            ClassificationMethod = ClassificationMethod.Manual,
            UploadedBy = userId,
        };
        db.Documents.Add(document);
        db.DocumentVersions.Add(new DocumentVersion
        {
            DocumentId = document.Id,
            VersionNumber = 1,
            FileName = fileName,
            FileSizeBytes = stored.SizeBytes,
            StorageKey = stored.StorageKey,
            IsCurrent = true,
            UploadedBy = userId,
            ExtractionStatus = "complete",
            ChangeNote = "Created in the document studio",
        });

        draft.DocumentId = document.Id;
        draft.Status = DraftStatus.Finalized;
        db.AuditLogs.Add(Audit(tenantId, userId, "DRAFT_FINALIZED", draft.Id));
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { draft.Id, documentId = document.Id, status = draft.Status.ToString() });
    }

    // ---- helpers ----

    private static async Task<Dictionary<string, string>> BuildTokensAsync(
        ApplicationDbContext db, Guid entityId, GenerateDraftRequest request, CancellationToken ct)
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var entity = await db.Entities.AsNoTracking().FirstOrDefaultAsync(e => e.Id == entityId, ct);
        if (entity is not null)
        {
            tokens["entity.name"] = entity.Name;
            tokens["entity.type"] = entity.Type.ToString();
            if (!string.IsNullOrWhiteSpace(entity.State)) tokens["entity.state"] = entity.State!;
            if (!string.IsNullOrWhiteSpace(entity.Ein)) tokens["entity.ein"] = entity.Ein!;
            if (!string.IsNullOrWhiteSpace(entity.Address)) tokens["entity.address"] = entity.Address!;
            if (!string.IsNullOrWhiteSpace(entity.Phone)) tokens["entity.phone"] = entity.Phone!;
            if (!string.IsNullOrWhiteSpace(entity.Website)) tokens["entity.website"] = entity.Website!;
        }

        if (!string.IsNullOrWhiteSpace(request.Recipient)) tokens["recipient"] = request.Recipient!;
        tokens["date"] = DateTime.UtcNow.ToString("MMMM d, yyyy");

        if (request.Variables is not null)
            foreach (var (k, v) in request.Variables)
                if (!string.IsNullOrWhiteSpace(k)) tokens[k] = v ?? string.Empty;

        return tokens;
    }

    private static async Task<List<DocumentTemplate>> EnsureTemplatesSeededAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var existing = await db.DocumentTemplates.ToListAsync(ct);
        if (existing.Count > 0) return existing;

        foreach (var seed in TemplateCatalog.Templates)
        {
            db.DocumentTemplates.Add(new DocumentTemplate
            {
                Key = seed.Key,
                Name = seed.Name,
                Category = seed.Category,
                Description = seed.Description,
                Body = seed.Body,
                IsSystem = true,
                CreatedBy = Guid.Empty,
            });
        }
        await db.SaveChangesAsync(ct);
        return await db.DocumentTemplates.ToListAsync(ct);
    }

    private static async Task<string> UniqueKeyAsync(ApplicationDbContext db, string baseKey, CancellationToken ct)
    {
        var key = string.IsNullOrEmpty(baseKey) ? "template" : baseKey;
        var candidate = key;
        var n = 2;
        while (await db.DocumentTemplates.AnyAsync(t => t.Key == candidate, ct))
            candidate = $"{key}-{n++}";
        return candidate;
    }

    private static DraftDetailDto ToDetail(DocumentDraft d) => new(
        d.Id, d.Title, d.TemplateKey, d.Prompt, d.Body, d.Status.ToString(), d.DocumentId,
        d.UpdatedAt == default ? d.CreatedAt : d.UpdatedAt);

    // Note: ToDetail runs on freshly-saved/tracked entities where UpdatedAt is stamped.

    private static string Slug(string text)
    {
        var chars = text.Trim().ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();
        var slug = new string(chars);
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');
        return slug.Length > 60 ? slug[..60].Trim('-') : slug;
    }

    private static AuditLog Audit(Guid tenantId, Guid? userId, string action, Guid resourceId) =>
        new()
        {
            TenantId = tenantId, UserId = userId, Action = action, ResourceType = "DocumentDraft", ResourceId = resourceId,
        };
}
