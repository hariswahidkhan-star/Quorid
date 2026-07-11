using Microsoft.EntityFrameworkCore;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Proposals;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Module 10 — Proposals &amp; Business Development (spec §Module 10). The proposal
/// builder (ordered documents + auto cover letter), the Kanban opportunity
/// tracker (stage transitions), and win/loss analytics.
/// </summary>
public static class ProposalEndpoints
{
    public static IEndpointRouteBuilder MapProposalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/proposals").WithTags("Proposals").RequireAuthorization();

        group.MapGet("/", ListAsync);
        group.MapGet("/analytics", AnalyticsAsync);
        group.MapPost("/", CreateAsync);
        group.MapGet("/{id:guid}", DetailAsync);
        group.MapPut("/{id:guid}", UpdateAsync);
        group.MapDelete("/{id:guid}", DeleteAsync);
        group.MapPost("/{id:guid}/documents", AddDocumentsAsync);
        group.MapPut("/{id:guid}/documents/reorder", ReorderAsync);
        group.MapDelete("/{id:guid}/documents/{proposalDocId:guid}", RemoveDocumentAsync);
        group.MapPost("/{id:guid}/cover-letter", GenerateCoverLetterAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var rows = await db.Proposals.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Id, p.Title, p.RecipientName, p.Stage, p.Value, p.DueDate, p.CreatedAt,
                Docs = db.ProposalDocuments.Count(pd => pd.ProposalId == p.Id),
            })
            .ToListAsync(ct);

        var list = rows.Select(p => new ProposalListItemDto(
            p.Id, p.Title, p.RecipientName, p.Stage.ToString(), p.Value, p.DueDate, p.Docs, p.CreatedAt)).ToList();

        return Results.Ok(list);
    }

    private static async Task<IResult> AnalyticsAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var proposals = await db.Proposals.AsNoTracking().Select(p => new { p.Stage, p.Value }).ToListAsync(ct);

        var won = proposals.Count(p => p.Stage == OpportunityStage.Won);
        var lost = proposals.Count(p => p.Stage == OpportunityStage.Lost);
        var open = proposals.Count - won - lost;
        var winRate = won + lost == 0 ? 0 : (int)Math.Round(100.0 * won / (won + lost));
        var pipeline = proposals
            .Where(p => p.Stage != OpportunityStage.Won && p.Stage != OpportunityStage.Lost)
            .Sum(p => p.Value ?? 0m);
        var wonValue = proposals.Where(p => p.Stage == OpportunityStage.Won).Sum(p => p.Value ?? 0m);

        return Results.Ok(new ProposalAnalyticsDto(proposals.Count, won, lost, open, winRate, pipeline, wonValue));
    }

    private static async Task<IResult> CreateAsync(
        CreateProposalRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest(new { error = "Title is required." });
        if (user.TenantId is not { } tenantId || user.UserId is not { } userId)
            return Results.Unauthorized();

        var proposal = new Proposal
        {
            TenantId = tenantId,
            EntityId = user.EntityId ?? Guid.Empty,
            Title = request.Title.Trim(),
            RecipientName = request.RecipientName,
            Value = request.Value,
            DueDate = request.DueDate,
            CoverLetter = request.CoverLetter,
            Stage = OpportunityStage.Lead,
            CreatedBy = userId,
        };
        db.Proposals.Add(proposal);
        db.AuditLogs.Add(Audit(tenantId, userId, "PROPOSAL_CREATED", proposal.Id));
        await db.SaveChangesAsync(ct);

        return Results.Ok(await BuildDetailAsync(proposal.Id, db, ct));
    }

    private static async Task<IResult> DetailAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var detail = await BuildDetailAsync(id, db, ct);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, UpdateProposalRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var proposal = await db.Proposals.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (proposal is null) return Results.NotFound();

        if (request.Title is not null) proposal.Title = request.Title;
        if (request.RecipientName is not null) proposal.RecipientName = request.RecipientName;
        if (request.Value is not null) proposal.Value = request.Value;
        if (request.CoverLetter is not null) proposal.CoverLetter = request.CoverLetter;
        if (request.DueDate is not null) proposal.DueDate = request.DueDate;
        if (request.Stage is not null && Enum.TryParse<OpportunityStage>(request.Stage, ignoreCase: true, out var stage))
            proposal.Stage = stage;

        await db.SaveChangesAsync(ct);
        return Results.Ok(new { proposal.Id, Stage = proposal.Stage.ToString() });
    }

    private static async Task<IResult> DeleteAsync(Guid id, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var proposal = await db.Proposals.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (proposal is null) return Results.NotFound();

        db.AuditLogs.Add(Audit(proposal.TenantId, user.UserId, "PROPOSAL_DELETED", id));
        db.Proposals.Remove(proposal); // cascades proposal documents
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AddDocumentsAsync(
        Guid id, AddProposalDocumentsRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (!await db.Proposals.AnyAsync(p => p.Id == id, ct)) return Results.NotFound();

        var existing = await db.ProposalDocuments.Where(pd => pd.ProposalId == id).ToListAsync(ct);
        var existingDocIds = existing.Select(x => x.DocumentId).ToHashSet();
        var order = existing.Count == 0 ? 0 : existing.Max(x => x.DisplayOrder);

        var validDocIds = await db.Documents
            .Where(d => request.DocumentIds.Contains(d.Id)).Select(d => d.Id).ToListAsync(ct);

        foreach (var docId in validDocIds.Where(docId => !existingDocIds.Contains(docId)))
        {
            db.ProposalDocuments.Add(new ProposalDocument
            {
                ProposalId = id, DocumentId = docId, DisplayOrder = ++order, AddedBy = user.UserId ?? Guid.Empty,
            });
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(await BuildDetailAsync(id, db, ct));
    }

    private static async Task<IResult> ReorderAsync(
        Guid id, ReorderRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.Proposals.AnyAsync(p => p.Id == id, ct)) return Results.NotFound();

        var items = await db.ProposalDocuments.Where(pd => pd.ProposalId == id).ToListAsync(ct);
        for (var i = 0; i < request.ProposalDocumentIds.Count; i++)
        {
            var item = items.FirstOrDefault(x => x.Id == request.ProposalDocumentIds[i]);
            if (item is not null) item.DisplayOrder = i;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(await BuildDetailAsync(id, db, ct));
    }

    private static async Task<IResult> RemoveDocumentAsync(Guid id, Guid proposalDocId, ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.Proposals.AnyAsync(p => p.Id == id, ct)) return Results.NotFound();

        var item = await db.ProposalDocuments.FirstOrDefaultAsync(pd => pd.Id == proposalDocId && pd.ProposalId == id, ct);
        if (item is null) return Results.NotFound();

        db.ProposalDocuments.Remove(item);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GenerateCoverLetterAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var proposal = await db.Proposals.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (proposal is null) return Results.NotFound();

        var entityName = await db.Entities.Where(e => e.Id == proposal.EntityId).Select(e => e.Name).FirstOrDefaultAsync(ct)
            ?? "our firm";

        proposal.CoverLetter = GenerateCoverLetter(entityName, proposal.Title, proposal.RecipientName);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { coverLetter = proposal.CoverLetter });
    }

    // ---- helpers ----

    private static async Task<ProposalDetailDto?> BuildDetailAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var proposal = await db.Proposals.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (proposal is null) return null;

        var docRows = await db.ProposalDocuments.AsNoTracking()
            .Where(pd => pd.ProposalId == id)
            .OrderBy(pd => pd.DisplayOrder)
            .Join(db.Documents, pd => pd.DocumentId, d => d.Id,
                (pd, d) => new { pd.Id, pd.DocumentId, d.FileName, d.Title, pd.DisplayOrder })
            .ToListAsync(ct);

        var docs = docRows
            .Select(x => new ProposalDocumentDto(x.Id, x.DocumentId, x.FileName, x.Title, x.DisplayOrder))
            .ToList();

        return new ProposalDetailDto(
            proposal.Id, proposal.Title, proposal.RecipientName, proposal.Stage.ToString(),
            proposal.Value, proposal.CoverLetter, proposal.DueDate, docs);
    }

    private static string GenerateCoverLetter(string entityName, string title, string? recipient)
    {
        var to = string.IsNullOrWhiteSpace(recipient) ? "Selection Committee" : recipient;
        return $"Dear {to},\n\n" +
               $"On behalf of {entityName}, we are pleased to submit our proposal, \"{title}.\" " +
               "The enclosed documents demonstrate our qualifications — each captured, classified, and verified " +
               "through Quorid's trust model — and our readiness to deliver. We would welcome the opportunity to " +
               "discuss how we can meet your requirements.\n\n" +
               $"Sincerely,\n{entityName}";
    }

    private static AuditLog Audit(Guid tenantId, Guid? userId, string action, Guid resourceId) =>
        new()
        {
            TenantId = tenantId, UserId = userId, Action = action, ResourceType = "Proposal", ResourceId = resourceId,
        };
}
