using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Compliance;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Module 6 — Compliance Engine (spec §Module 6). Framework catalog with
/// real-time scoring, gap analysis, and a deadline calendar. Evidence is computed
/// live by matching an entity's documents against each requirement.
/// </summary>
public static class ComplianceEndpoints
{
    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/compliance").WithTags("Compliance").RequireAuthorization();

        group.MapGet("/frameworks", FrameworksAsync);
        group.MapPost("/frameworks", CreateFrameworkAsync);
        group.MapGet("/frameworks/{id:guid}/status", FrameworkStatusAsync);
        group.MapGet("/overview", OverviewAsync);
        group.MapGet("/calendar", CalendarAsync);

        return app;
    }

    private static async Task<IResult> FrameworksAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var frameworks = await EnsureSeededAsync(db, ct);
        var ids = frameworks.Select(f => f.Id).ToList();

        var counts = (await db.ComplianceRequirements.AsNoTracking()
                .Where(r => ids.Contains(r.FrameworkId))
                .GroupBy(r => r.FrameworkId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.Count);

        var list = frameworks.Select(f => new FrameworkDto(
            f.Id, f.Code, f.Name, f.Description, f.IsSystem,
            counts.TryGetValue(f.Id, out var c) ? c : 0)).ToList();

        return Results.Ok(list);
    }

    private static async Task<IResult> CreateFrameworkAsync(
        CreateFrameworkRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Framework name is required." });

        var framework = new ComplianceFramework
        {
            Name = request.Name.Trim(),
            Code = GenerateCode(request.Name),
            Description = request.Description,
            IsSystem = false,
            Status = "active",
        };
        db.ComplianceFrameworks.Add(framework);

        var count = 0;
        if (request.Requirements is { Count: > 0 })
        {
            foreach (var r in request.Requirements.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                db.ComplianceRequirements.Add(new ComplianceRequirement
                {
                    FrameworkId = framework.Id,
                    Name = r.Name.Trim(),
                    RequiredDocTypes = JsonSerializer.Serialize(r.Domains ?? new List<string>()),
                    MinVerificationTier = ParseTier(r.MinTier),
                });
                count++;
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = user.TenantId ?? Guid.Empty,
            UserId = user.UserId,
            Action = "FRAMEWORK_CREATED",
            ResourceType = nameof(ComplianceFramework),
            ResourceId = framework.Id,
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new FrameworkDto(framework.Id, framework.Code, framework.Name, framework.Description, false, count));
    }

    private static async Task<IResult> FrameworkStatusAsync(
        Guid id, ApplicationDbContext db, ICurrentUser user, Guid? entityId, CancellationToken ct)
    {
        var framework = await db.ComplianceFrameworks.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (framework is null) return Results.NotFound();
        if ((entityId ?? user.EntityId) is not { } eid)
            return Results.BadRequest(new { error = "No entity specified." });

        var requirements = await db.ComplianceRequirements.AsNoTracking()
            .Where(r => r.FrameworkId == id).ToListAsync(ct);
        var infos = requirements.Select(ToInfo).ToList();

        var docs = await LoadDocsAsync(db, eid, ct);
        var (statuses, score) = ComplianceEvaluator.Evaluate(infos, docs, Today());
        var present = statuses.Count(s => s.Status == "present");

        var reqDtos = statuses
            .Select(s => new RequirementStatusDto(s.RequirementId, s.Name, s.Status, s.DocumentId, s.DocumentTitle))
            .ToList();

        return Results.Ok(new FrameworkStatusDto(framework.Id, framework.Code, framework.Name, score, present, infos.Count, reqDtos));
    }

    private static async Task<IResult> OverviewAsync(
        ApplicationDbContext db, ICurrentUser user, Guid? entityId, CancellationToken ct)
    {
        if ((entityId ?? user.EntityId) is not { } eid)
            return Results.BadRequest(new { error = "No entity specified." });

        var frameworks = await EnsureSeededAsync(db, ct);
        var ids = frameworks.Select(f => f.Id).ToList();
        var allReqs = await db.ComplianceRequirements.AsNoTracking().Where(r => ids.Contains(r.FrameworkId)).ToListAsync(ct);
        var byFramework = allReqs.GroupBy(r => r.FrameworkId).ToDictionary(g => g.Key, g => g.ToList());

        var docs = await LoadDocsAsync(db, eid, ct);
        var today = Today();

        var overviews = new List<FrameworkOverviewDto>();
        foreach (var framework in frameworks)
        {
            var infos = (byFramework.TryGetValue(framework.Id, out var rs) ? rs : new List<ComplianceRequirement>())
                .Select(ToInfo).ToList();
            var (statuses, score) = ComplianceEvaluator.Evaluate(infos, docs, today);
            var present = statuses.Count(s => s.Status == "present");
            overviews.Add(new FrameworkOverviewDto(framework.Id, framework.Code, framework.Name, score, present, infos.Count));
        }

        var overall = overviews.Count == 0 ? 0 : (int)Math.Round(overviews.Average(o => o.Score));
        return Results.Ok(new ComplianceOverviewDto(eid, overall, overviews));
    }

    private static async Task<IResult> CalendarAsync(
        ApplicationDbContext db, ICurrentUser user, Guid? entityId, CancellationToken ct)
    {
        if ((entityId ?? user.EntityId) is not { } eid)
            return Results.BadRequest(new { error = "No entity specified." });

        var frameworks = await EnsureSeededAsync(db, ct);
        var frameworkNames = frameworks.ToDictionary(f => f.Id, f => f.Name);
        var ids = frameworks.Select(f => f.Id).ToList();
        var requirements = await db.ComplianceRequirements.AsNoTracking().Where(r => ids.Contains(r.FrameworkId)).ToListAsync(ct);

        var docs = await LoadDocsAsync(db, eid, ct);
        var today = Today();

        var items = new List<ComplianceCalendarItemDto>();
        foreach (var req in requirements)
        {
            var info = ToInfo(req);
            var match = docs
                .Where(d => d.DomainCode != null && info.Domains.Contains(d.DomainCode) &&
                            d.Tier >= info.MinTier && d.ExpiryDate is not null)
                .OrderBy(d => d.ExpiryDate)
                .FirstOrDefault();

            if (match?.ExpiryDate is { } expiry)
            {
                items.Add(new ComplianceCalendarItemDto(
                    frameworkNames.TryGetValue(req.FrameworkId, out var name) ? name : "",
                    req.Name, match.Id, match.Title ?? match.FileName, expiry, expiry.DayNumber - today.DayNumber));
            }
        }

        return Results.Ok(items.OrderBy(i => i.ExpiryDate).ToList());
    }

    // ---- helpers ----

    private static async Task<List<ComplianceFramework>> EnsureSeededAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var existing = await db.ComplianceFrameworks.ToListAsync(ct);
        if (existing.Count > 0) return existing;

        var created = new List<ComplianceFramework>();
        foreach (var seed in FrameworkCatalog.Frameworks)
        {
            var framework = new ComplianceFramework
            {
                Name = seed.Name, Code = seed.Code, Description = seed.Description, IsSystem = true, Status = "active",
            };
            db.ComplianceFrameworks.Add(framework);

            foreach (var reqSeed in seed.Requirements)
            {
                db.ComplianceRequirements.Add(new ComplianceRequirement
                {
                    FrameworkId = framework.Id,
                    Name = reqSeed.Name,
                    RequiredDocTypes = JsonSerializer.Serialize(reqSeed.Domains),
                    MinVerificationTier = ParseTier(reqSeed.MinTier),
                });
            }

            created.Add(framework);
        }

        await db.SaveChangesAsync(ct);
        return created;
    }

    private static async Task<List<ComplianceDoc>> LoadDocsAsync(ApplicationDbContext db, Guid entityId, CancellationToken ct)
    {
        var rows = await db.Documents.AsNoTracking()
            .Where(d => d.EntityId == entityId && d.Status != DocumentStatus.Archived)
            .Select(d => new { d.Id, d.Title, d.FileName, d.DomainCode, d.VerificationTier, d.ExpiryDate })
            .ToListAsync(ct);

        return rows
            .Select(d => new ComplianceDoc(d.Id, d.Title, d.FileName, d.DomainCode, d.VerificationTier, d.ExpiryDate))
            .ToList();
    }

    private static RequirementInfo ToInfo(ComplianceRequirement r) =>
        new(r.Id, r.Name, DeserializeDomains(r.RequiredDocTypes), r.MinVerificationTier);

    private static string[] DeserializeDomains(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static VerificationTier ParseTier(string? tier) =>
        Enum.TryParse<VerificationTier>(tier, ignoreCase: true, out var t) ? t : VerificationTier.T1;

    private static string GenerateCode(string name)
    {
        var letters = new string(name.Where(char.IsLetterOrDigit).Take(8).ToArray()).ToUpperInvariant();
        return string.IsNullOrEmpty(letters) ? "CUSTOM" : letters;
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
}
