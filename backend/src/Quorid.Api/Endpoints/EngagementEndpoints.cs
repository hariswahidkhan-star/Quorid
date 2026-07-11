using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Engagements;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Modules 7–9 — Projects, Vendors, and Clients (spec §Modules 7–9). One unified
/// engagement model with a type discriminator: a document checklist with progress
/// tracking, plus an external upload portal token for vendors/clients.
/// </summary>
public static class EngagementEndpoints
{
    public static IEndpointRouteBuilder MapEngagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/engagements").WithTags("Engagements").RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:guid}", DetailAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPost("/{id:guid}/checklist", AddChecklistAsync);
        group.MapPut("/{id:guid}/checklist/{itemId:guid}/assign", AssignAsync);
        group.MapDelete("/{id:guid}/checklist/{itemId:guid}", RemoveItemAsync);
        group.MapPost("/{id:guid}/collection-request", CollectionRequestAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(ApplicationDbContext db, string? type, CancellationToken ct)
    {
        IQueryable<Engagement> query = db.Engagements.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<EngagementType>(type, ignoreCase: true, out var t))
            query = query.Where(e => e.Type == t);

        var rows = await query
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new
            {
                e.Id, e.Type, e.Name, e.ContactEmail, e.Status, e.DueDate, e.CreatedAt,
                Total = db.ChecklistItems.Count(c => c.EngagementId == e.Id),
                Received = db.ChecklistItems.Count(c => c.EngagementId == e.Id && c.DocumentId != null),
            })
            .ToListAsync(ct);

        var list = rows.Select(e => new EngagementListItemDto(
            e.Id, e.Type.ToString(), e.Name, e.ContactEmail, e.Status.ToString(), e.DueDate,
            e.Total, e.Received, Progress(e.Total, e.Received), e.CreatedAt)).ToList();

        return Results.Ok(list);
    }

    private static async Task<IResult> CreateAsync(
        CreateEngagementRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Name is required." });
        if (!Enum.TryParse<EngagementType>(request.Type, ignoreCase: true, out var type))
            return Results.BadRequest(new { error = "Invalid engagement type." });
        if (user.TenantId is not { } tenantId || user.UserId is not { } userId)
            return Results.Unauthorized();

        var engagement = new Engagement
        {
            TenantId = tenantId,
            EntityId = user.EntityId ?? Guid.Empty,
            Type = type,
            Name = request.Name.Trim(),
            ContactEmail = request.ContactEmail,
            Status = EngagementStatus.Active,
            DueDate = request.DueDate,
            AccessToken = type == EngagementType.Project ? null : GenerateToken(),
            CreatedBy = userId,
        };
        db.Engagements.Add(engagement);

        if (request.Checklist is { Count: > 0 })
        {
            foreach (var item in request.Checklist.Where(i => !string.IsNullOrWhiteSpace(i.Name)))
            {
                db.ChecklistItems.Add(new ChecklistItem
                {
                    EngagementId = engagement.Id,
                    Name = item.Name.Trim(),
                    DomainCode = item.DomainCode,
                    IsRequired = item.IsRequired ?? true,
                });
            }
        }

        db.AuditLogs.Add(Audit(tenantId, userId, "ENGAGEMENT_CREATED", engagement.Id));
        await db.SaveChangesAsync(ct);

        return Results.Ok(await BuildDetailAsync(engagement.Id, db, ct));
    }

    private static async Task<IResult> DetailAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var detail = await BuildDetailAsync(id, db, ct);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateEngagementRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var engagement = await db.Engagements.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (engagement is null) return Results.NotFound();

        if (request.Name is not null) engagement.Name = request.Name;
        if (request.DueDate is not null) engagement.DueDate = request.DueDate;
        if (request.Status is not null && Enum.TryParse<EngagementStatus>(request.Status, ignoreCase: true, out var st))
            engagement.Status = st;

        await db.SaveChangesAsync(ct);
        return Results.Ok(new { engagement.Id, Status = engagement.Status.ToString() });
    }

    private static async Task<IResult> DeleteAsync(Guid id, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var engagement = await db.Engagements.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (engagement is null) return Results.NotFound();

        db.AuditLogs.Add(Audit(engagement.TenantId, user.UserId, "ENGAGEMENT_DELETED", id));
        db.Engagements.Remove(engagement); // cascades checklist items
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AddChecklistAsync(
        Guid id, AddChecklistRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.Engagements.AnyAsync(e => e.Id == id, ct)) return Results.NotFound();

        foreach (var item in request.Items.Where(i => !string.IsNullOrWhiteSpace(i.Name)))
        {
            db.ChecklistItems.Add(new ChecklistItem
            {
                EngagementId = id,
                Name = item.Name.Trim(),
                DomainCode = item.DomainCode,
                IsRequired = item.IsRequired ?? true,
            });
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(await BuildDetailAsync(id, db, ct));
    }

    private static async Task<IResult> AssignAsync(
        Guid id, Guid itemId, AssignDocumentRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.Engagements.AnyAsync(e => e.Id == id, ct)) return Results.NotFound();

        var item = await db.ChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId && c.EngagementId == id, ct);
        if (item is null) return Results.NotFound();

        if (request.DocumentId is { } docId)
        {
            if (!await db.Documents.AnyAsync(d => d.Id == docId, ct))
                return Results.BadRequest(new { error = "Document not found." });
            item.DocumentId = docId;
            item.ReceivedAt = DateTime.UtcNow;
        }
        else
        {
            item.DocumentId = null;
            item.ReceivedAt = null;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(await BuildDetailAsync(id, db, ct));
    }

    private static async Task<IResult> RemoveItemAsync(Guid id, Guid itemId, ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.Engagements.AnyAsync(e => e.Id == id, ct)) return Results.NotFound();

        var item = await db.ChecklistItems.FirstOrDefaultAsync(c => c.Id == itemId && c.EngagementId == id, ct);
        if (item is null) return Results.NotFound();

        db.ChecklistItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CollectionRequestAsync(
        Guid id, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var engagement = await db.Engagements.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (engagement is null) return Results.NotFound();
        if (engagement.Type == EngagementType.Project)
            return Results.BadRequest(new { error = "Projects do not have an external portal." });

        engagement.AccessToken ??= GenerateToken();
        db.AuditLogs.Add(Audit(engagement.TenantId, user.UserId, "COLLECTION_REQUEST_SENT", id));
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { token = engagement.AccessToken });
    }

    // ---- helpers ----

    private static async Task<EngagementDetailDto?> BuildDetailAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var engagement = await db.Engagements.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);
        if (engagement is null) return null;

        var items = await db.ChecklistItems.AsNoTracking().Where(c => c.EngagementId == id).ToListAsync(ct);

        var docIds = items.Where(c => c.DocumentId != null).Select(c => c.DocumentId!.Value).Distinct().ToList();
        var titles = docIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await db.Documents.AsNoTracking()
                    .Where(d => docIds.Contains(d.Id))
                    .Select(d => new { d.Id, Title = d.Title ?? d.FileName })
                    .ToListAsync(ct))
                .ToDictionary(x => x.Id, x => x.Title);

        var checklist = items.Select(c => new ChecklistItemDto(
            c.Id, c.Name, c.DomainCode, c.IsRequired,
            c.DocumentId != null ? "received" : "pending",
            c.DocumentId,
            c.DocumentId != null && titles.TryGetValue(c.DocumentId.Value, out var t) ? t : null,
            c.ReceivedAt)).ToList();

        var total = items.Count;
        var received = items.Count(c => c.DocumentId != null);

        return new EngagementDetailDto(
            engagement.Id, engagement.Type.ToString(), engagement.Name, engagement.ContactEmail,
            engagement.Status.ToString(), engagement.DueDate, engagement.AccessToken,
            total, received, Progress(total, received), checklist);
    }

    private static int Progress(int total, int received) =>
        total == 0 ? 0 : (int)Math.Round(100.0 * received / total);

    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    private static AuditLog Audit(Guid tenantId, Guid? userId, string action, Guid resourceId) =>
        new()
        {
            TenantId = tenantId, UserId = userId, Action = action, ResourceType = "Engagement", ResourceId = resourceId,
        };
}
