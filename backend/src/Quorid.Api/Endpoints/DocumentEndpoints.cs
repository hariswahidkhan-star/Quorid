using Microsoft.EntityFrameworkCore;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Read endpoints for the document vault / File Browser (spec §7.2, §Module 3).
/// The tenant query filter scopes every result automatically.
/// </summary>
public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Documents").RequireAuthorization();

        group.MapGet("/", async (
            ApplicationDbContext db,
            string? domain, string? status, string? search, Guid? entityId,
            int page = 1, int pageSize = 20) =>
        {
            page = page < 1 ? 1 : page;
            pageSize = Math.Clamp(pageSize, 1, 100);

            IQueryable<Document> query = db.Documents.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(domain))
                query = query.Where(d => d.DomainCode == domain);

            if (!string.IsNullOrWhiteSpace(status) &&
                Enum.TryParse<DocumentStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                query = query.Where(d => d.Status == parsedStatus);
            }
            else
            {
                // By default hide archived (soft-deleted) documents.
                query = query.Where(d => d.Status != DocumentStatus.Archived);
            }

            if (entityId is { } eid)
                query = query.Where(d => d.EntityId == eid);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(d => d.FileName.Contains(search) ||
                                         (d.Title != null && d.Title.Contains(search)));

            query = query.OrderByDescending(d => d.CreatedAt);

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(d => new
                {
                    d.Id,
                    d.FileName,
                    d.Title,
                    d.FileType,
                    d.FileSizeBytes,
                    d.Status,
                    d.DomainCode,
                    d.VerificationTier,
                    d.PrivacyLevel,
                    d.EntityId,
                    d.ExpiryDate,
                    d.CreatedAt,
                })
                .ToListAsync();

            return Results.Ok(new { page, pageSize, total, items });
        });

        group.MapGet("/{id:guid}", async (Guid id, ApplicationDbContext db) =>
        {
            var doc = await db.Documents.AsNoTracking()
                .Where(d => d.Id == id)
                .Select(d => new
                {
                    d.Id,
                    d.FileName,
                    d.Title,
                    d.Description,
                    d.FileType,
                    d.FileSizeBytes,
                    d.Version,
                    d.Status,
                    d.DomainCode,
                    d.CategoryCode,
                    d.TypeCode,
                    d.SubtypeCode,
                    d.PrivacyLevel,
                    d.VerificationTier,
                    d.ExpiryDate,
                    d.EntityId,
                    d.ClassificationConfidence,
                    d.CreatedAt,
                    d.UpdatedAt,
                    ExtractedFields = d.ExtractedFields.Select(f => new
                    {
                        f.Id,
                        f.FieldName,
                        f.FieldValue,
                        f.OverrideValue,
                        f.Confidence,
                        f.SourcePage,
                    }),
                    Tags = d.Tags.Select(t => new { t.TagName, t.Source }),
                })
                .FirstOrDefaultAsync();

            return doc is null ? Results.NotFound() : Results.Ok(doc);
        });

        return app;
    }
}
