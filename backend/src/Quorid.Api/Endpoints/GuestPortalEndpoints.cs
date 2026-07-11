using Microsoft.EntityFrameworkCore;
using Quorid.Application.Rooms;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Data Rooms — guest side (spec §7.4). Tokenized, unauthenticated access with
/// an NDA gate, room content, page-level view logging, and Q&amp;A. OTP email
/// verification and MFA from the full flow are deferred (they need email/TOTP
/// infrastructure); first access is auto-verified here.
/// </summary>
public static class GuestPortalEndpoints
{
    public static IEndpointRouteBuilder MapGuestPortalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/guest/room/{token}", GetGuestRoomAsync).AllowAnonymous().WithTags("Guest Portal");
        app.MapPost("/api/guest/room/{token}/sign-nda", SignNdaAsync).AllowAnonymous().WithTags("Guest Portal");
        app.MapPost("/api/guest/room/{token}/view", LogViewAsync).AllowAnonymous().WithTags("Guest Portal");
        app.MapGet("/api/guest/room/{token}/qa", GuestQaListAsync).AllowAnonymous().WithTags("Guest Portal");
        app.MapPost("/api/guest/room/{token}/qa", GuestSubmitQaAsync).AllowAnonymous().WithTags("Guest Portal");
        return app;
    }

    private static async Task<IResult> GetGuestRoomAsync(
        string token, ApplicationDbContext db, CancellationToken ct)
    {
        var guest = await db.RoomGuests.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.AccessToken == token, ct);
        if (guest is null) return Results.NotFound(new { error = "Invalid access link." });
        if (guest.Status == GuestStatus.Revoked)
            return Results.Json(new { error = "Your access to this room has been revoked." }, statusCode: 403);

        var room = await db.DataRooms.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == guest.RoomId, ct);
        if (room is null) return Results.NotFound(new { error = "Room not found." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (room.Status is RoomStatus.Closed or RoomStatus.Archived or RoomStatus.Draft || room.ExpiryDate < today)
            return Results.Json(new { error = "This data room is not currently available." }, statusCode: 410);

        var host = await db.Entities.IgnoreQueryFilters()
            .Where(e => e.Id == room.EntityId).Select(e => e.Name).FirstOrDefaultAsync(ct) ?? string.Empty;

        // First access: mark verified/active (OTP + MFA deferred).
        guest.FirstAccessAt ??= DateTime.UtcNow;
        guest.LastAccessAt = DateTime.UtcNow;
        guest.EmailVerified = true;
        if (guest.Status == GuestStatus.Invited) guest.Status = GuestStatus.Active;
        await db.SaveChangesAsync(ct);

        var ndaSigned = guest.NdaSignedAt != null;
        var folders = new List<GuestFolderDto>();

        if (!room.NdaRequired || ndaSigned)
        {
            var folderList = await db.RoomFolders.AsNoTracking()
                .Where(f => f.RoomId == room.Id).OrderBy(f => f.DisplayOrder).ToListAsync(ct);

            var docRows = await db.RoomDocuments.AsNoTracking().IgnoreQueryFilters()
                .Where(rd => rd.RoomId == room.Id)
                .Join(db.Documents, rd => rd.DocumentId, d => d.Id,
                    (rd, d) => new { rd.FolderId, d.Id, d.FileName, d.FileType, d.FileSizeBytes })
                .ToListAsync(ct);

            var byFolder = docRows.GroupBy(x => x.FolderId).ToDictionary(g => g.Key, g => g.ToList());

            folders = folderList.Select(f => new GuestFolderDto(
                f.Id, f.Name,
                (byFolder.TryGetValue(f.Id, out var ds) ? ds : new())
                    .Select(d => new GuestDocDto(d.Id, d.FileName, d.FileType, d.FileSizeBytes)).ToList()))
                .ToList();
        }

        return Results.Ok(new GuestRoomDto(
            room.Name, host, room.ExpiryDate, room.NdaRequired, ndaSigned,
            room.WatermarkEnabled, room.WatermarkText, room.DownloadPolicy.ToString(), room.QaEnabled,
            guest.Name, guest.Email, guest.PermissionLevel.ToString(), folders));
    }

    private static async Task<IResult> SignNdaAsync(
        string token, SignNdaRequest request, HttpContext http, ApplicationDbContext db, CancellationToken ct)
    {
        var guest = await db.RoomGuests.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.AccessToken == token, ct);
        if (guest is null) return Results.NotFound(new { error = "Invalid access link." });
        if (string.IsNullOrWhiteSpace(request.LegalName))
            return Results.BadRequest(new { error = "Your full legal name is required." });

        guest.NdaSignedAt = DateTime.UtcNow;
        guest.NdaSignerName = request.LegalName.Trim();
        guest.NdaIpAddress = http.Connection.RemoteIpAddress?.ToString();
        guest.Status = GuestStatus.Active;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { signed = true });
    }

    private static async Task<IResult> LogViewAsync(
        string token, LogViewRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var guest = await db.RoomGuests.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.AccessToken == token, ct);
        if (guest is null || guest.Status == GuestStatus.Revoked)
            return Results.NotFound(new { error = "Invalid access link." });

        var inRoom = await db.RoomDocuments.IgnoreQueryFilters()
            .AnyAsync(rd => rd.RoomId == guest.RoomId && rd.DocumentId == request.DocumentId, ct);
        if (!inRoom) return Results.NotFound(new { error = "Document not in this room." });

        db.DocumentViews.Add(new DocumentView
        {
            GuestId = guest.Id,
            DocumentId = request.DocumentId,
            RoomId = guest.RoomId,
            PageNumber = request.Page,
            ViewedAt = DateTime.UtcNow,
            Action = DocumentViewAction.View,
        });
        guest.LastAccessAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { logged = true });
    }

    private static async Task<IResult> GuestQaListAsync(string token, ApplicationDbContext db, CancellationToken ct)
    {
        var guest = await db.RoomGuests.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.AccessToken == token, ct);
        if (guest is null) return Results.NotFound(new { error = "Invalid access link." });

        var rows = await db.QaQuestions.AsNoTracking()
            .Where(q => q.RoomId == guest.RoomId &&
                        (q.GuestId == guest.Id || (q.IsPublic && q.Status == QaStatus.Published)))
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(ct);

        var list = rows.Select(q => new GuestQuestionDto(
            q.Id, q.Category.ToString(), q.QuestionText, q.Status.ToString(), q.AnswerText, q.CreatedAt)).ToList();

        return Results.Ok(list);
    }

    private static async Task<IResult> GuestSubmitQaAsync(
        string token, SubmitQuestionRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var guest = await db.RoomGuests.IgnoreQueryFilters().FirstOrDefaultAsync(g => g.AccessToken == token, ct);
        if (guest is null || guest.Status == GuestStatus.Revoked)
            return Results.NotFound(new { error = "Invalid access link." });

        var room = await db.DataRooms.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == guest.RoomId, ct);
        if (room is null) return Results.NotFound(new { error = "Room not found." });
        if (!room.QaEnabled) return Results.BadRequest(new { error = "Q&A is disabled for this room." });
        if (string.IsNullOrWhiteSpace(request.QuestionText))
            return Results.BadRequest(new { error = "Question text is required." });

        var category = Enum.TryParse<QaCategory>(request.Category, ignoreCase: true, out var c) ? c : QaCategory.General;

        db.QaQuestions.Add(new QaQuestion
        {
            RoomId = guest.RoomId,
            GuestId = guest.Id,
            Category = category,
            QuestionText = request.QuestionText.Trim(),
            Status = QaStatus.Pending,
        });
        await db.SaveChangesAsync(ct);

        return Results.Ok(new { submitted = true });
    }
}
