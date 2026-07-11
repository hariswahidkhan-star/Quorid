using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Quorid.Application.Analytics;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Compliance;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Module 11 — Analytics &amp; Reporting (spec §Module 11). A live KPI dashboard,
/// the natural-language "Ask Quorid" assistant, and a catalog of 18 pre-built
/// reports. Everything aggregates real tenant data through the global query filter.
/// </summary>
public static class AnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/analytics").WithTags("Analytics").RequireAuthorization();

        group.MapGet("/dashboard", DashboardAsync);
        group.MapPost("/ask", AskAsync);
        group.MapGet("/reports", ListReports);
        group.MapGet("/reports/{key}", RunReportAsync);

        return app;
    }

    // ---- Dashboard ----

    private static async Task<IResult> DashboardAsync(ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var s = await BuildSnapshotAsync(db, user, ct);

        var kpis = new List<KpiDto>
        {
            new("documents", "Documents", s.TotalDocuments.ToString(), $"{s.ActiveDocuments} active", "neutral"),
            new("verified", "Verified", $"{s.VerifiedPercent}%", "Tier 3+", Tone(s.VerifiedPercent, 60, 30)),
            new("expiring", "Expiring ≤30d", s.ExpiringSoon.ToString(), "documents", s.ExpiringSoon == 0 ? "positive" : "critical"),
            new("compliance", "Compliance", s.ComplianceScore is { } c ? $"{c}%" : "—", "readiness",
                s.ComplianceScore is { } cs ? Tone(cs, 70, 40) : "neutral"),
            new("rooms", "Active rooms", s.ActiveRooms.ToString(), $"{s.TotalGuests} guests", "neutral"),
            new("shares", "Active shares", s.ActiveShares.ToString(), $"{s.TotalShareViews} views", "neutral"),
            new("engagements", "Engagements", s.ActiveEngagements.ToString(), "active", "neutral"),
            new("pipeline", "Pipeline", Money(s.PipelineValue), $"{s.WinRatePercent}% win rate", "positive"),
        };

        var breakdowns = new List<BreakdownDto>
        {
            new("documents-by-status", "Documents by status", s.DocumentsByStatus),
            new("proposals-by-stage", "Proposals by stage", s.ProposalsByStage),
        };

        var activity = (await db.AuditLogs.AsNoTracking()
                .OrderByDescending(a => a.CreatedAt)
                .Take(8)
                .Select(a => new { a.Action, a.ResourceType, a.CreatedAt })
                .ToListAsync(ct))
            .Select(a => new ActivityDto(a.Action, a.ResourceType, a.CreatedAt))
            .ToList();

        return Results.Ok(new DashboardDto(kpis, breakdowns, activity));
    }

    // ---- Ask Quorid ----

    private static async Task<IResult> AskAsync(
        AskRequest request, ApplicationDbContext db, ICurrentUser user, IAnalyticsAssistant assistant, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Results.BadRequest(new { error = "Ask a question." });

        var snapshot = await BuildSnapshotAsync(db, user, ct);
        var result = await assistant.AskAsync(request.Question, snapshot, ct);

        return Results.Ok(new AskResponseDto(
            result.Answer, result.Metric, result.Value,
            result.Breakdown ?? Array.Empty<BreakdownSliceDto>(),
            result.Suggestions ?? Array.Empty<string>()));
    }

    // ---- Reports ----

    private static IResult ListReports() =>
        Results.Ok(ReportCatalog.Reports
            .Select(r => new ReportSummaryDto(r.Key, r.Title, r.Category, r.Description))
            .ToList());

    private static async Task<IResult> RunReportAsync(string key, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var def = ReportCatalog.Find(key);
        if (def is null) return Results.NotFound(new { error = "Unknown report." });

        var (columns, rows) = key switch
        {
            "documents-by-status" => await DocumentsGroupedAsync(db, d => d.Status.ToString(), ct),
            "documents-by-tier" => await DocumentsGroupedAsync(db, d => TierLabel(d.VerificationTier), ct),
            "documents-by-domain" => await DocumentsGroupedAsync(db, d => d.DomainCode ?? "Unclassified", ct),
            "documents-by-privacy" => await DocumentsGroupedAsync(db, d => d.PrivacyLevel.ToString(), ct),
            "documents-expiring" => await DocumentsExpiringAsync(db, ct),
            "documents-by-entity" => await DocumentsByEntityAsync(db, ct),
            "compliance-scorecard" => await ComplianceScorecardAsync(db, user, ct),
            "compliance-gaps" => await ComplianceGapsAsync(db, user, ct),
            "compliance-renewals" => await ComplianceRenewalsAsync(db, user, ct),
            "shares-active" => await SharesActiveAsync(db, ct),
            "shares-engagement" => await SharesEngagementAsync(db, ct),
            "shares-by-permission" => await SharesByPermissionAsync(db, ct),
            "rooms-summary" => await RoomsSummaryAsync(db, ct),
            "rooms-guest-activity" => await RoomsGuestActivityAsync(db, ct),
            "engagements-pipeline" => await EngagementsPipelineAsync(db, ct),
            "engagements-checklist" => await EngagementsChecklistAsync(db, ct),
            "proposals-pipeline" => await ProposalsPipelineAsync(db, ct),
            "proposals-winloss" => await ProposalsWinLossAsync(db, ct),
            _ => ((IReadOnlyList<string>)new[] { "Info" },
                  new List<IReadOnlyList<string>> { new[] { "Not implemented." } }),
        };

        return Results.Ok(new ReportResultDto(def.Key, def.Title, def.Category, columns, rows, rows.Count));
    }

    // ---- Report implementations ----

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> DocumentsGroupedAsync(
        ApplicationDbContext db, Func<DocProjection, string> selector, CancellationToken ct)
    {
        var docs = await LoadDocProjectionAsync(db, ct);
        var total = docs.Count;
        var rows = docs
            .GroupBy(selector)
            .OrderByDescending(g => g.Count())
            .Select(g => (IReadOnlyList<string>)new[] { g.Key, g.Count().ToString(), Percent(g.Count(), total) })
            .ToList();
        return (new[] { "Group", "Count", "Share" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> DocumentsExpiringAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var today = Today();
        var horizon = today.AddDays(90);
        var rows = (await db.Documents.AsNoTracking()
                .Where(d => d.Status != DocumentStatus.Archived && d.ExpiryDate != null &&
                            d.ExpiryDate >= today && d.ExpiryDate <= horizon)
                .Select(d => new { d.Title, d.FileName, d.DomainCode, d.ExpiryDate })
                .ToListAsync(ct))
            .OrderBy(d => d.ExpiryDate)
            .Select(d => (IReadOnlyList<string>)new[]
            {
                d.Title ?? d.FileName, d.DomainCode ?? "—",
                d.ExpiryDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                (d.ExpiryDate!.Value.DayNumber - today.DayNumber) + " days",
            })
            .ToList();
        return (new[] { "Document", "Domain", "Expires", "In" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> DocumentsByEntityAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var counts = await db.Documents.AsNoTracking()
            .Where(d => d.Status != DocumentStatus.Archived)
            .GroupBy(d => d.EntityId)
            .Select(g => new { EntityId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var names = await db.Entities.AsNoTracking().Select(e => new { e.Id, e.Name }).ToListAsync(ct);
        var nameById = names.ToDictionary(x => x.Id, x => x.Name);

        var rows = counts
            .OrderByDescending(c => c.Count)
            .Select(c => (IReadOnlyList<string>)new[]
            {
                nameById.TryGetValue(c.EntityId, out var n) ? n : "(unknown)", c.Count.ToString(),
            })
            .ToList();
        return (new[] { "Entity", "Documents" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> ComplianceScorecardAsync(
        ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var (frameworks, byFramework, docs, today) = await LoadComplianceAsync(db, user, ct);
        var scored = new List<(string Name, int Score, int Present, int Total)>();
        foreach (var f in frameworks)
        {
            var infos = (byFramework.TryGetValue(f.Id, out var rs) ? rs : new List<ComplianceRequirement>()).Select(ToInfo).ToList();
            var (statuses, score) = ComplianceEvaluator.Evaluate(infos, docs, today);
            scored.Add((f.Name, score, statuses.Count(x => x.Status == "present"), infos.Count));
        }

        var rows = scored
            .OrderByDescending(x => x.Score)
            .Select(x => (IReadOnlyList<string>)new[] { x.Name, $"{x.Score}%", $"{x.Present}/{x.Total}" })
            .ToList();
        return (new[] { "Framework", "Score", "Requirements met" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> ComplianceGapsAsync(
        ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var (frameworks, byFramework, docs, today) = await LoadComplianceAsync(db, user, ct);
        var rows = new List<IReadOnlyList<string>>();
        foreach (var f in frameworks)
        {
            var infos = (byFramework.TryGetValue(f.Id, out var rs) ? rs : new List<ComplianceRequirement>()).Select(ToInfo).ToList();
            var (statuses, _) = ComplianceEvaluator.Evaluate(infos, docs, today);
            foreach (var st in statuses.Where(x => x.Status != "present"))
                rows.Add(new[] { f.Name, st.Name, st.Status });
        }
        return (new[] { "Framework", "Requirement", "Status" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> ComplianceRenewalsAsync(
        ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var (frameworks, byFramework, docs, today) = await LoadComplianceAsync(db, user, ct);
        var horizon = today.AddDays(120);
        var names = frameworks.ToDictionary(f => f.Id, f => f.Name);
        var rows = new List<IReadOnlyList<string>>();

        foreach (var (frameworkId, reqs) in byFramework)
        {
            foreach (var req in reqs)
            {
                var info = ToInfo(req);
                var match = docs
                    .Where(d => d.DomainCode != null && info.Domains.Contains(d.DomainCode) &&
                                d.Tier >= info.MinTier && d.ExpiryDate is not null &&
                                d.ExpiryDate >= today && d.ExpiryDate <= horizon)
                    .OrderBy(d => d.ExpiryDate)
                    .FirstOrDefault();
                if (match?.ExpiryDate is { } expiry)
                    rows.Add(new[]
                    {
                        names.TryGetValue(frameworkId, out var n) ? n : "", req.Name,
                        match.Title ?? match.FileName, expiry.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    });
            }
        }
        return (new[] { "Framework", "Requirement", "Document", "Expires" },
            rows.OrderBy(r => r[3]).ToList());
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> SharesActiveAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var rows = (await db.SharingRecords.AsNoTracking()
                .Where(r => r.Status == SharingStatus.Active)
                .Select(r => new { r.RecipientEmail, r.RecipientName, r.PermissionLevel, r.ViewCount, r.ExpiryDate })
                .ToListAsync(ct))
            .OrderByDescending(r => r.ViewCount)
            .Select(r => (IReadOnlyList<string>)new[]
            {
                r.RecipientName ?? r.RecipientEmail, r.PermissionLevel.ToString(), r.ViewCount.ToString(),
                r.ExpiryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—",
            })
            .ToList();
        return (new[] { "Recipient", "Permission", "Views", "Expires" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> SharesEngagementAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var rows = (await db.SharingRecords.AsNoTracking()
                .Select(r => new { r.RecipientEmail, r.RecipientName, r.ViewCount, r.LastViewedAt, r.Status })
                .ToListAsync(ct))
            .OrderByDescending(r => r.ViewCount)
            .Select(r => (IReadOnlyList<string>)new[]
            {
                r.RecipientName ?? r.RecipientEmail, r.ViewCount.ToString(),
                r.LastViewedAt?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "never",
                r.Status.ToString(),
            })
            .ToList();
        return (new[] { "Recipient", "Views", "Last viewed", "Status" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> SharesByPermissionAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var shares = await db.SharingRecords.AsNoTracking().Select(r => new { r.PermissionLevel }).ToListAsync(ct);
        var total = shares.Count;
        var rows = shares
            .GroupBy(r => r.PermissionLevel)
            .OrderBy(g => g.Key)
            .Select(g => (IReadOnlyList<string>)new[] { g.Key.ToString(), g.Count().ToString(), Percent(g.Count(), total) })
            .ToList();
        return (new[] { "Permission", "Shares", "Share" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> RoomsSummaryAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var rooms = await db.DataRooms.AsNoTracking()
            .Select(r => new { r.Id, r.Name, r.Status, r.ExpiryDate }).ToListAsync(ct);
        var roomIds = rooms.Select(r => r.Id).ToList();

        var guestCounts = (await db.RoomGuests.AsNoTracking()
                .Where(g => roomIds.Contains(g.RoomId))
                .Select(g => g.RoomId).ToListAsync(ct))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var docCounts = (await db.RoomDocuments.AsNoTracking()
                .Where(rd => roomIds.Contains(rd.RoomId))
                .Select(rd => rd.RoomId).ToListAsync(ct))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var rows = rooms
            .OrderByDescending(r => guestCounts.TryGetValue(r.Id, out var gc) ? gc : 0)
            .Select(r => (IReadOnlyList<string>)new[]
            {
                r.Name, r.Status.ToString(),
                (guestCounts.TryGetValue(r.Id, out var gc) ? gc : 0).ToString(),
                (docCounts.TryGetValue(r.Id, out var dc) ? dc : 0).ToString(),
                r.ExpiryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            })
            .ToList();
        return (new[] { "Room", "Status", "Guests", "Documents", "Expires" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> RoomsGuestActivityAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var rooms = await db.DataRooms.AsNoTracking().Select(r => new { r.Id, r.Name }).ToListAsync(ct);
        var nameById = rooms.ToDictionary(r => r.Id, r => r.Name);
        var roomIds = rooms.Select(r => r.Id).ToList();

        var views = (await db.DocumentViews.AsNoTracking()
                .Where(v => roomIds.Contains(v.RoomId))
                .Select(v => new { v.RoomId, v.DurationSeconds }).ToListAsync(ct))
            .GroupBy(v => v.RoomId)
            .Select(g => new { RoomId = g.Key, Views = g.Count(), Seconds = g.Sum(x => x.DurationSeconds) })
            .OrderByDescending(x => x.Views)
            .ToList();

        var rows = views
            .Select(v => (IReadOnlyList<string>)new[]
            {
                nameById.TryGetValue(v.RoomId, out var n) ? n : "(unknown)", v.Views.ToString(),
                TimeSpan.FromSeconds(v.Seconds).ToString(@"hh\:mm\:ss"),
            })
            .ToList();
        return (new[] { "Room", "View events", "Time on documents" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> EngagementsPipelineAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var engagements = await db.Engagements.AsNoTracking().Select(e => new { e.Type, e.Status }).ToListAsync(ct);
        var rows = engagements
            .GroupBy(e => new { e.Type, e.Status })
            .OrderBy(g => g.Key.Type).ThenBy(g => g.Key.Status)
            .Select(g => (IReadOnlyList<string>)new[]
            {
                g.Key.Type.ToString(), g.Key.Status.ToString(), g.Count().ToString(),
            })
            .ToList();
        return (new[] { "Type", "Status", "Count" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> EngagementsChecklistAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var engagements = await db.Engagements.AsNoTracking()
            .Select(e => new { e.Id, e.Name, e.Type }).ToListAsync(ct);
        var engIds = engagements.Select(e => e.Id).ToList();

        var items = (await db.ChecklistItems.AsNoTracking()
                .Where(c => engIds.Contains(c.EngagementId))
                .Select(c => new { c.EngagementId, Received = c.DocumentId != null }).ToListAsync(ct))
            .GroupBy(c => c.EngagementId)
            .ToDictionary(g => g.Key, g => new { Total = g.Count(), Received = g.Count(x => x.Received) });

        var rows = engagements
            .Select(e =>
            {
                var stat = items.TryGetValue(e.Id, out var v) ? v : new { Total = 0, Received = 0 };
                var ratio = stat.Total == 0 ? 0.0 : (double)stat.Received / stat.Total;
                return (Ratio: ratio, Cells: (IReadOnlyList<string>)new[]
                {
                    e.Name, e.Type.ToString(), $"{stat.Received}/{stat.Total}", Percent(stat.Received, stat.Total),
                });
            })
            .OrderByDescending(r => r.Ratio)
            .Select(r => r.Cells)
            .ToList();
        return (new[] { "Engagement", "Type", "Received", "Complete" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> ProposalsPipelineAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var proposals = await db.Proposals.AsNoTracking().Select(p => new { p.Stage, p.Value }).ToListAsync(ct);
        var rows = Enum.GetValues<OpportunityStage>()
            .Select(stage =>
            {
                var inStage = proposals.Where(p => p.Stage == stage).ToList();
                return (IReadOnlyList<string>)new[]
                {
                    stage.ToString(), inStage.Count.ToString(), Money(inStage.Sum(p => p.Value ?? 0m)),
                };
            })
            .ToList();
        return (new[] { "Stage", "Count", "Value" }, rows);
    }

    private static async Task<(IReadOnlyList<string>, List<IReadOnlyList<string>>)> ProposalsWinLossAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var proposals = await db.Proposals.AsNoTracking().Select(p => new { p.Stage, p.Value }).ToListAsync(ct);
        var won = proposals.Where(p => p.Stage == OpportunityStage.Won).ToList();
        var lost = proposals.Where(p => p.Stage == OpportunityStage.Lost).ToList();
        var open = proposals.Where(p => p.Stage != OpportunityStage.Won && p.Stage != OpportunityStage.Lost).ToList();
        var winRate = won.Count + lost.Count == 0 ? 0 : (int)Math.Round(100.0 * won.Count / (won.Count + lost.Count));

        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Won", won.Count.ToString(), Money(won.Sum(p => p.Value ?? 0m)) },
            new[] { "Lost", lost.Count.ToString(), Money(lost.Sum(p => p.Value ?? 0m)) },
            new[] { "Open", open.Count.ToString(), Money(open.Sum(p => p.Value ?? 0m)) },
            new[] { "Win rate", $"{winRate}%", "—" },
        };
        return (new[] { "Outcome", "Count", "Value" }, rows);
    }

    // ---- Snapshot ----

    private static async Task<MetricSnapshot> BuildSnapshotAsync(
        ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var docs = await db.Documents.AsNoTracking()
            .Where(d => d.Status != DocumentStatus.Archived)
            .Select(d => new { d.Status, d.VerificationTier, d.ExpiryDate })
            .ToListAsync(ct);

        var today = Today();
        var soon = today.AddDays(30);
        var total = docs.Count;
        var active = docs.Count(d => d.Status == DocumentStatus.Active);
        var expiring = docs.Count(d => d.ExpiryDate is { } e && e >= today && e <= soon);
        var verified = docs.Count(d => d.VerificationTier >= VerificationTier.T3);
        var verifiedPct = total == 0 ? 0 : (int)Math.Round(100.0 * verified / total);

        var docsByStatus = docs
            .GroupBy(d => d.Status)
            .OrderBy(g => g.Key)
            .Select(g => new BreakdownSliceDto(g.Key.ToString(), g.Count()))
            .ToList();

        var activeRooms = await db.DataRooms.CountAsync(r => r.Status == RoomStatus.Active, ct);
        var roomIds = await db.DataRooms.AsNoTracking().Select(r => r.Id).ToListAsync(ct);
        var totalGuests = await db.RoomGuests.CountAsync(g => roomIds.Contains(g.RoomId), ct);

        var activeShares = await db.SharingRecords.CountAsync(r => r.Status == SharingStatus.Active, ct);
        var totalViews = await db.SharingRecords.AsNoTracking().SumAsync(r => (int?)r.ViewCount, ct) ?? 0;

        var activeEngagements = await db.Engagements.CountAsync(e => e.Status == EngagementStatus.Active, ct);

        var proposals = await db.Proposals.AsNoTracking().Select(p => new { p.Stage, p.Value }).ToListAsync(ct);
        var won = proposals.Count(p => p.Stage == OpportunityStage.Won);
        var lost = proposals.Count(p => p.Stage == OpportunityStage.Lost);
        var openProposals = proposals.Count - won - lost;
        var winRate = won + lost == 0 ? 0 : (int)Math.Round(100.0 * won / (won + lost));
        var pipeline = proposals
            .Where(p => p.Stage != OpportunityStage.Won && p.Stage != OpportunityStage.Lost)
            .Sum(p => p.Value ?? 0m);
        var proposalsByStage = Enum.GetValues<OpportunityStage>()
            .Select(stage => new BreakdownSliceDto(
                stage.ToString(),
                proposals.Count(p => p.Stage == stage),
                proposals.Where(p => p.Stage == stage).Sum(p => p.Value ?? 0m)))
            .ToList();

        var complianceScore = await ComputeComplianceScoreAsync(db, user, ct);

        return new MetricSnapshot(
            total, active, expiring, verifiedPct,
            activeRooms, totalGuests, activeShares, totalViews,
            activeEngagements, openProposals, pipeline, winRate,
            complianceScore, docsByStatus, proposalsByStage);
    }

    private static async Task<int?> ComputeComplianceScoreAsync(
        ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (user.EntityId is not { } entityId) return null;

        var frameworks = await db.ComplianceFrameworks.AsNoTracking().Select(f => f.Id).ToListAsync(ct);
        if (frameworks.Count == 0) return null;

        var reqs = await db.ComplianceRequirements.AsNoTracking()
            .Where(r => frameworks.Contains(r.FrameworkId)).ToListAsync(ct);
        var byFramework = reqs.GroupBy(r => r.FrameworkId).ToDictionary(g => g.Key, g => g.ToList());
        var docs = await LoadComplianceDocsAsync(db, entityId, ct);
        var today = Today();

        var scores = new List<int>();
        foreach (var frameworkId in frameworks)
        {
            var infos = (byFramework.TryGetValue(frameworkId, out var rs) ? rs : new List<ComplianceRequirement>())
                .Select(ToInfo).ToList();
            var (_, score) = ComplianceEvaluator.Evaluate(infos, docs, today);
            scores.Add(score);
        }
        return scores.Count == 0 ? null : (int)Math.Round(scores.Average());
    }

    // ---- Shared helpers ----

    private record DocProjection(DocumentStatus Status, VerificationTier VerificationTier, string? DomainCode, PrivacyLevel PrivacyLevel);

    private static async Task<List<DocProjection>> LoadDocProjectionAsync(ApplicationDbContext db, CancellationToken ct) =>
        await db.Documents.AsNoTracking()
            .Where(d => d.Status != DocumentStatus.Archived)
            .Select(d => new DocProjection(d.Status, d.VerificationTier, d.DomainCode, d.PrivacyLevel))
            .ToListAsync(ct);

    private static async Task<(List<ComplianceFramework> Frameworks,
        Dictionary<Guid, List<ComplianceRequirement>> ByFramework,
        List<ComplianceDoc> Docs, DateOnly Today)> LoadComplianceAsync(
        ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var frameworks = await db.ComplianceFrameworks.AsNoTracking().ToListAsync(ct);
        var ids = frameworks.Select(f => f.Id).ToList();
        var reqs = await db.ComplianceRequirements.AsNoTracking().Where(r => ids.Contains(r.FrameworkId)).ToListAsync(ct);
        var byFramework = reqs.GroupBy(r => r.FrameworkId).ToDictionary(g => g.Key, g => g.ToList());

        var docs = user.EntityId is { } entityId
            ? await LoadComplianceDocsAsync(db, entityId, ct)
            : new List<ComplianceDoc>();

        return (frameworks, byFramework, docs, Today());
    }

    private static async Task<List<ComplianceDoc>> LoadComplianceDocsAsync(
        ApplicationDbContext db, Guid entityId, CancellationToken ct)
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
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static string TierLabel(VerificationTier tier) => tier switch
    {
        VerificationTier.T1 => "T1 · Self-Reported",
        VerificationTier.T2 => "T2 · Cross-Referenced",
        VerificationTier.T3 => "T3 · Source-Verified",
        VerificationTier.T4 => "T4 · Counter-Party",
        _ => tier.ToString(),
    };

    private static string Tone(int value, int good, int warn) =>
        value >= good ? "positive" : value >= warn ? "neutral" : "critical";

    private static string Percent(int part, int whole) =>
        whole == 0 ? "0%" : Math.Round(100.0 * part / whole) + "%";

    private static string Money(decimal v) => "$" + v.ToString("N0", CultureInfo.InvariantCulture);

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);
}
