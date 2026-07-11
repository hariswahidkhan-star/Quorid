using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Sharing;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Module 5 — Sharing &amp; Access (spec §Module 5 / §7.2). The 8-level permission
/// model, a tokenized public share link with view analytics, and privacy-level
/// enforcement (Locked documents cannot be shared).
/// </summary>
public static class SharingEndpoints
{
    public static IEndpointRouteBuilder MapSharingEndpoints(this IEndpointRouteBuilder app)
    {
        var docs = app.MapGroup("/api/documents").WithTags("Sharing").RequireAuthorization();
        docs.MapPost("/{id:guid}/share", CreateShareAsync);
        docs.MapGet("/{id:guid}/shares", DocumentSharesAsync);

        var shares = app.MapGroup("/api/shares").WithTags("Sharing").RequireAuthorization();
        shares.MapGet("/", ListSharesAsync);
        shares.MapPost("/{id:guid}/revoke", RevokeAsync);
        shares.MapGet("/{id:guid}/analytics", AnalyticsAsync);

        // Public, tokenized recipient view.
        app.MapGet("/api/share/{token}", PublicViewAsync).AllowAnonymous().WithTags("Sharing");

        return app;
    }

    private static async Task<IResult> CreateShareAsync(
        Guid id, CreateShareRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientEmail))
            return Results.BadRequest(new { error = "Recipient email is required." });

        if (!Enum.TryParse<PermissionLevel>(request.PermissionLevel, ignoreCase: true, out var permission))
            return Results.BadRequest(new { error = "Invalid permission level." });

        var doc = await db.Documents.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (doc is null) return Results.NotFound();
        if (doc.PrivacyLevel == PrivacyLevel.Locked)
            return Results.BadRequest(new { error = "Locked documents cannot be shared." });

        var share = new SharingRecord
        {
            TenantId = doc.TenantId,
            DocumentId = id,
            RecipientEmail = request.RecipientEmail.Trim().ToLowerInvariant(),
            RecipientName = request.RecipientName,
            PermissionLevel = permission,
            ExpiryDate = request.ExpiryDate,
            Watermark = request.Watermark ?? true,
            TrackViews = request.TrackViews ?? true,
            Message = request.Message,
            AccessToken = GenerateToken(),
            Status = SharingStatus.Active,
            CreatedBy = user.UserId ?? Guid.Empty,
        };
        db.SharingRecords.Add(share);
        db.AuditLogs.Add(Audit(doc.TenantId, user.UserId, "DOCUMENT_SHARED", id));
        await db.SaveChangesAsync(ct);

        return Results.Ok(ToDto(share, doc.Title ?? doc.FileName));
    }

    private static async Task<IResult> ListSharesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var rows = await db.SharingRecords.AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .Join(db.Documents, s => s.DocumentId, d => d.Id,
                (s, d) => new { Share = s, DocTitle = d.Title ?? d.FileName })
            .ToListAsync(ct);

        return Results.Ok(rows.Select(x => ToDto(x.Share, x.DocTitle)).ToList());
    }

    private static async Task<IResult> DocumentSharesAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.Documents.AnyAsync(d => d.Id == id, ct)) return Results.NotFound();

        var shares = await db.SharingRecords.AsNoTracking()
            .Where(s => s.DocumentId == id)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        return Results.Ok(shares.Select(s => ToDto(s, null)).ToList());
    }

    private static async Task<IResult> RevokeAsync(
        Guid id, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var share = await db.SharingRecords.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (share is null) return Results.NotFound();

        share.Status = SharingStatus.Revoked;
        db.AuditLogs.Add(Audit(share.TenantId, user.UserId, "SHARE_REVOKED", share.Id));
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { share.Id, Status = share.Status.ToString() });
    }

    private static async Task<IResult> AnalyticsAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var share = await db.SharingRecords.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (share is null) return Results.NotFound();

        var views = await db.ShareViews.AsNoTracking()
            .Where(v => v.SharingRecordId == id)
            .OrderByDescending(v => v.ViewedAt)
            .Take(200)
            .Select(v => new ShareViewDto(v.IpAddress, v.UserAgent, v.ViewedAt))
            .ToListAsync(ct);

        return Results.Ok(new ShareAnalyticsDto(share.Id, share.ViewCount, share.LastViewedAt, views));
    }

    private static async Task<IResult> PublicViewAsync(
        string token, HttpContext http, ApplicationDbContext db, CancellationToken ct)
    {
        var share = await db.SharingRecords.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.AccessToken == token, ct);

        if (share is null) return Results.NotFound(new { error = "Share not found." });
        if (share.Status != SharingStatus.Active)
            return Results.Json(new { error = "This share is no longer available." }, statusCode: 410);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (share.ExpiryDate is { } expiry && expiry < today)
        {
            share.Status = SharingStatus.Expired;
            await db.SaveChangesAsync(ct);
            return Results.Json(new { error = "This share has expired." }, statusCode: 410);
        }

        var doc = await db.Documents.IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Id == share.DocumentId, ct);
        if (doc is null) return Results.NotFound(new { error = "Document not found." });

        if (share.TrackViews)
        {
            db.ShareViews.Add(new ShareView
            {
                SharingRecordId = share.Id,
                IpAddress = http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                UserAgent = http.Request.Headers.UserAgent.ToString(),
                ViewedAt = DateTime.UtcNow,
            });
            share.ViewCount += 1;
            share.LastViewedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        var message = share.Watermark
            ? $"Watermarked for {share.RecipientEmail}. Rendered securely — download only if permitted."
            : "Shared document.";

        return Results.Ok(new PublicShareDto(
            doc.Title ?? doc.FileName, doc.FileType, share.PermissionLevel.ToString(), share.Watermark, message));
    }

    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static ShareDto ToDto(SharingRecord s, string? documentTitle) =>
        new(s.Id, s.DocumentId, documentTitle, s.RecipientEmail, s.RecipientName,
            s.PermissionLevel.ToString(), s.Status.ToString(), s.ExpiryDate, s.Watermark, s.TrackViews,
            s.ViewCount, s.LastViewedAt, s.AccessToken, s.CreatedAt);

    private static AuditLog Audit(Guid tenantId, Guid? userId, string action, Guid resourceId) =>
        new()
        {
            TenantId = tenantId,
            UserId = userId,
            Action = action,
            ResourceType = "Sharing",
            ResourceId = resourceId,
        };
}
