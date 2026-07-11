using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Rooms;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Data Rooms — admin side (spec §3, §7.3). Room types + templates, the builder
/// wizard's create call, room CRUD, guest invites, Q&amp;A answering, and analytics.
/// The guest-facing portal lives in <see cref="GuestPortalEndpoints"/>.
/// </summary>
public static class RoomEndpoints
{
    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rooms").WithTags("Rooms").RequireAuthorization();

        group.MapGet("/room-types", RoomTypesAsync);
        group.MapGet("/", ListRoomsAsync);
        group.MapPost("/", CreateRoomAsync);
        group.MapGet("/{id:guid}", RoomDetailAsync);
        group.MapPut("/{id:guid}", UpdateRoomAsync);
        group.MapDelete("/{id:guid}", DeleteRoomAsync);
        group.MapPost("/{id:guid}/guests", InviteGuestsAsync);
        group.MapDelete("/{id:guid}/guests/{guestId:guid}", RevokeGuestAsync);
        group.MapGet("/{id:guid}/analytics", AnalyticsAsync);
        group.MapGet("/{id:guid}/qa", ListQuestionsAsync);
        group.MapPut("/{id:guid}/qa/{questionId:guid}/answer", AnswerQuestionAsync);

        return app;
    }

    private static async Task<IResult> RoomTypesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var types = await EnsureRoomTypesSeededAsync(db, ct);
        var result = types
            .Select(t => new RoomTypeDto(
                t.Id, t.Code, t.Name, t.Description,
                RoomTemplateCatalog.ByCode(t.Code)?.Folders ?? Array.Empty<string>()))
            .ToList();
        return Results.Ok(result);
    }

    private static async Task<IResult> ListRoomsAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var codeMap = (await db.RoomTypes.AsNoTracking().Select(t => new { t.Id, t.Code }).ToListAsync(ct))
            .ToDictionary(x => x.Id, x => x.Code);

        var rows = await db.DataRooms.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new
            {
                r.Id, r.Name, r.Status, r.RoomTypeId, r.ExpiryDate, r.CreatedAt,
                Docs = db.RoomDocuments.Count(rd => rd.RoomId == r.Id),
                Guests = db.RoomGuests.Count(g => g.RoomId == r.Id),
            })
            .ToListAsync(ct);

        var list = rows.Select(r => new RoomListItemDto(
            r.Id, r.Name, r.Status.ToString(),
            codeMap.TryGetValue(r.RoomTypeId, out var code) ? code : "",
            r.ExpiryDate, r.Docs, r.Guests, r.CreatedAt)).ToList();

        return Results.Ok(list);
    }

    private static async Task<IResult> CreateRoomAsync(
        CreateRoomRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Room name is required." });
        if (user.TenantId is not { } tenantId || user.UserId is not { } userId)
            return Results.Unauthorized();

        var entityId = request.EntityId ?? user.EntityId;
        if (entityId is not { } eid)
            return Results.BadRequest(new { error = "No entity specified." });

        var types = await EnsureRoomTypesSeededAsync(db, ct);
        var roomType = types.FirstOrDefault(t => t.Code == request.RoomTypeCode);
        if (roomType is null) return Results.BadRequest(new { error = "Unknown room type." });

        var downloadPolicy = Enum.TryParse<DownloadPolicy>(request.DownloadPolicy, ignoreCase: true, out var dp)
            ? dp
            : DownloadPolicy.ViewOnly;

        var room = new DataRoom
        {
            TenantId = tenantId,
            EntityId = eid,
            Name = request.Name.Trim(),
            RoomTypeId = roomType.Id,
            Status = RoomStatus.Active,
            ExpiryDate = request.ExpiryDate,
            NdaRequired = request.NdaRequired ?? true,
            WatermarkEnabled = request.Watermark ?? true,
            WatermarkText = request.WatermarkText ?? "CONFIDENTIAL",
            QaEnabled = request.QaEnabled ?? true,
            DownloadPolicy = downloadPolicy,
            CreatedBy = userId,
        };
        db.DataRooms.Add(room);

        var template = RoomTemplateCatalog.ByCode(request.RoomTypeCode);
        var folderNames = request.Folders is { Count: > 0 }
            ? request.Folders
            : template?.Folders ?? Array.Empty<string>();

        var folders = new List<RoomFolder>();
        var order = 1;
        foreach (var name in folderNames)
        {
            var folder = new RoomFolder { RoomId = room.Id, Name = name, DisplayOrder = order++ };
            folders.Add(folder);
            db.RoomFolders.Add(folder);
        }

        var firstFolder = folders.FirstOrDefault();
        if (request.DocumentIds is { Count: > 0 } && firstFolder is not null)
        {
            var validDocIds = await db.Documents
                .Where(d => request.DocumentIds.Contains(d.Id))
                .Select(d => d.Id)
                .ToListAsync(ct);

            foreach (var docId in validDocIds)
            {
                db.RoomDocuments.Add(new RoomDocument
                {
                    RoomId = room.Id, FolderId = firstFolder.Id, DocumentId = docId, AddedBy = userId,
                });
            }
        }

        if (request.Guests is { Count: > 0 })
            AddGuests(db, room.Id, request.Guests, userId);

        db.AuditLogs.Add(Audit(tenantId, userId, "ROOM_CREATED", room.Id));
        await db.SaveChangesAsync(ct);

        return Results.Ok(await BuildDetailAsync(room.Id, db, ct));
    }

    private static async Task<IResult> RoomDetailAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var detail = await BuildDetailAsync(id, db, ct);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    private static async Task<IResult> UpdateRoomAsync(
        Guid id, UpdateRoomRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var room = await db.DataRooms.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (room is null) return Results.NotFound();

        if (request.Name is not null) room.Name = request.Name;
        if (request.ExpiryDate is not null) room.ExpiryDate = request.ExpiryDate.Value;
        if (request.QaEnabled is not null) room.QaEnabled = request.QaEnabled.Value;
        if (request.Watermark is not null) room.WatermarkEnabled = request.Watermark.Value;
        if (request.Status is not null && Enum.TryParse<RoomStatus>(request.Status, ignoreCase: true, out var st))
            room.Status = st;
        if (request.DownloadPolicy is not null &&
            Enum.TryParse<DownloadPolicy>(request.DownloadPolicy, ignoreCase: true, out var dp))
            room.DownloadPolicy = dp;

        db.AuditLogs.Add(Audit(room.TenantId, user.UserId, "ROOM_UPDATED", room.Id));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { room.Id, Status = room.Status.ToString() });
    }

    private static async Task<IResult> DeleteRoomAsync(
        Guid id, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var room = await db.DataRooms.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (room is null) return Results.NotFound();
        if (room.Status == RoomStatus.Active)
            return Results.BadRequest(new { error = "Close the room before deleting it." });

        db.DocumentViews.RemoveRange(await db.DocumentViews.Where(v => v.RoomId == id).ToListAsync(ct));
        db.QaQuestions.RemoveRange(await db.QaQuestions.Where(q => q.RoomId == id).ToListAsync(ct));
        db.GuestSessions.RemoveRange(await db.GuestSessions.Where(s => s.RoomId == id).ToListAsync(ct));
        db.AuditLogs.Add(Audit(room.TenantId, user.UserId, "ROOM_DELETED", room.Id));
        db.DataRooms.Remove(room); // cascades folders, room_documents, room_guests

        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> InviteGuestsAsync(
        Guid id, InviteGuestsRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        var room = await db.DataRooms.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (room is null) return Results.NotFound();
        if (user.UserId is not { } userId) return Results.Unauthorized();

        var existing = await db.RoomGuests.Where(g => g.RoomId == id).Select(g => g.Email).ToListAsync(ct);
        var seen = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        var toAdd = request.Guests
            .Where(g => !string.IsNullOrWhiteSpace(g.Email) && seen.Add(g.Email.Trim().ToLowerInvariant()))
            .ToList();
        AddGuests(db, id, toAdd, userId);

        db.AuditLogs.Add(Audit(room.TenantId, user.UserId, "ROOM_GUESTS_INVITED", room.Id));
        await db.SaveChangesAsync(ct);

        var detail = await BuildDetailAsync(id, db, ct);
        return Results.Ok(detail);
    }

    private static async Task<IResult> RevokeGuestAsync(
        Guid id, Guid guestId, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (!await db.DataRooms.AnyAsync(r => r.Id == id, ct)) return Results.NotFound();

        var guest = await db.RoomGuests.FirstOrDefaultAsync(g => g.Id == guestId && g.RoomId == id, ct);
        if (guest is null) return Results.NotFound();

        guest.Status = GuestStatus.Revoked;
        db.AuditLogs.Add(Audit(user.TenantId ?? Guid.Empty, user.UserId, "ROOM_GUEST_REVOKED", guestId));
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> AnalyticsAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.DataRooms.AnyAsync(r => r.Id == id, ct)) return Results.NotFound();

        var guests = await db.RoomGuests.AsNoTracking().Where(g => g.RoomId == id).ToListAsync(ct);
        var views = await db.DocumentViews.AsNoTracking().Where(v => v.RoomId == id).ToListAsync(ct);
        var byGuest = views.GroupBy(v => v.GuestId).ToDictionary(g => g.Key, g => g.Count());

        var engagement = guests.Select(g =>
        {
            var count = byGuest.TryGetValue(g.Id, out var c) ? c : 0;
            var label = count == 0 ? "Cold" : count < 10 ? "Warm" : "Hot";
            return new GuestEngagementDto(g.Id, g.Email, g.Name, count, g.LastAccessAt, label);
        }).ToList();

        return Results.Ok(new RoomAnalyticsDto(id, guests.Count, views.Count, engagement));
    }

    private static async Task<IResult> ListQuestionsAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        if (!await db.DataRooms.AnyAsync(r => r.Id == id, ct)) return Results.NotFound();

        var rows = await db.QaQuestions.AsNoTracking()
            .Where(q => q.RoomId == id)
            .OrderByDescending(q => q.CreatedAt)
            .Join(db.RoomGuests, q => q.GuestId, g => g.Id, (q, g) => new { Q = q, g.Email })
            .ToListAsync(ct);

        var list = rows.Select(x => new RoomQuestionDto(
            x.Q.Id, x.Email, x.Q.Category.ToString(), x.Q.QuestionText,
            x.Q.Status.ToString(), x.Q.AnswerText, x.Q.IsPublic, x.Q.CreatedAt)).ToList();

        return Results.Ok(list);
    }

    private static async Task<IResult> AnswerQuestionAsync(
        Guid id, Guid questionId, AnswerQuestionRequest request, ApplicationDbContext db, ICurrentUser user, CancellationToken ct)
    {
        if (!await db.DataRooms.AnyAsync(r => r.Id == id, ct)) return Results.NotFound();

        var question = await db.QaQuestions.FirstOrDefaultAsync(q => q.Id == questionId && q.RoomId == id, ct);
        if (question is null) return Results.NotFound();

        question.AnswerText = request.AnswerText;
        question.AnsweredBy = user.UserId;
        question.AnsweredAt = DateTime.UtcNow;
        question.Status = QaStatus.Published;
        question.IsPublic = request.IsPublic ?? false;

        await db.SaveChangesAsync(ct);
        return Results.Ok(new { question.Id, Status = question.Status.ToString() });
    }

    // ---- helpers ----

    private static void AddGuests(ApplicationDbContext db, Guid roomId, IReadOnlyList<RoomGuestInput> guests, Guid invitedBy)
    {
        foreach (var g in guests.Where(x => !string.IsNullOrWhiteSpace(x.Email)))
        {
            var role = Enum.TryParse<RoomGuestRole>(g.Role, ignoreCase: true, out var r) ? r : RoomGuestRole.Reviewer;
            var permission = Enum.TryParse<PermissionLevel>(g.PermissionLevel, ignoreCase: true, out var p) ? p : PermissionLevel.View;

            db.RoomGuests.Add(new RoomGuest
            {
                RoomId = roomId,
                Email = g.Email.Trim().ToLowerInvariant(),
                Name = g.Name ?? string.Empty,
                Role = role,
                PermissionLevel = permission,
                AccessToken = GenerateToken(),
                Status = GuestStatus.Invited,
                InvitedBy = invitedBy,
            });
        }
    }

    private static async Task<RoomDetailDto?> BuildDetailAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var room = await db.DataRooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (room is null) return null;

        var typeCode = await db.RoomTypes.AsNoTracking()
            .Where(t => t.Id == room.RoomTypeId).Select(t => t.Code).FirstOrDefaultAsync(ct) ?? "";

        var folders = await db.RoomFolders.AsNoTracking()
            .Where(f => f.RoomId == id).OrderBy(f => f.DisplayOrder)
            .Select(f => new RoomFolderDto(f.Id, f.Name, f.DisplayOrder))
            .ToListAsync(ct);

        var docRows = await db.RoomDocuments.AsNoTracking()
            .Where(rd => rd.RoomId == id)
            .Join(db.Documents, rd => rd.DocumentId, d => d.Id,
                (rd, d) => new { rd.Id, rd.DocumentId, d.FileName, d.Title, rd.FolderId, rd.PermissionOverride })
            .ToListAsync(ct);

        var documents = docRows.Select(x => new RoomDocumentDto(
            x.Id, x.DocumentId, x.FileName, x.Title, x.FolderId,
            x.PermissionOverride?.ToString() ?? room.DownloadPolicy.ToString())).ToList();

        var guestRows = await db.RoomGuests.AsNoTracking().Where(g => g.RoomId == id).ToListAsync(ct);
        var viewMap = (await db.DocumentViews.AsNoTracking().Where(v => v.RoomId == id)
                .GroupBy(v => v.GuestId).Select(g => new { GuestId = g.Key, Count = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.GuestId, x => x.Count);

        var guests = guestRows.Select(g => new RoomGuestDto(
            g.Id, g.Email, g.Name, g.Role.ToString(), g.PermissionLevel.ToString(), g.Status.ToString(),
            g.NdaSignedAt != null, viewMap.TryGetValue(g.Id, out var vc) ? vc : 0, g.LastAccessAt, g.AccessToken)).ToList();

        return new RoomDetailDto(
            room.Id, room.Name, room.Description, room.Status.ToString(), typeCode, room.ExpiryDate,
            room.NdaRequired, room.WatermarkEnabled, room.WatermarkText, room.QaEnabled,
            room.DownloadPolicy.ToString(), folders, documents, guests);
    }

    private static async Task<List<RoomType>> EnsureRoomTypesSeededAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var existing = await db.RoomTypes.ToListAsync(ct);
        if (existing.Count > 0) return existing;

        var created = new List<RoomType>();
        foreach (var seed in RoomTemplateCatalog.Types)
        {
            var roomType = new RoomType
            {
                Name = seed.Name, Code = seed.Code, Description = seed.Description, IsSystem = true, Status = "active",
            };
            db.RoomTypes.Add(roomType);
            db.RoomTemplates.Add(new RoomTemplate
            {
                RoomTypeId = roomType.Id,
                Name = $"{seed.Name} Standard",
                FolderStructure = JsonSerializer.Serialize(seed.Folders),
                SuggestedDocTypes = "[]",
            });
            created.Add(roomType);
        }

        await db.SaveChangesAsync(ct);
        return created;
    }

    private static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

    private static AuditLog Audit(Guid tenantId, Guid? userId, string action, Guid resourceId) =>
        new()
        {
            TenantId = tenantId, UserId = userId, Action = action, ResourceType = "DataRoom", ResourceId = resourceId,
        };
}
