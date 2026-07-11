namespace Quorid.Application.Rooms;

// ---- Create / update ----

public record RoomGuestInput(string Email, string? Name, string? Role, string? PermissionLevel);

public record CreateRoomRequest(
    string Name,
    Guid? EntityId,
    string RoomTypeCode,
    DateOnly ExpiryDate,
    bool? NdaRequired,
    bool? Watermark,
    string? WatermarkText,
    bool? QaEnabled,
    string? DownloadPolicy,
    IReadOnlyList<string>? Folders,
    IReadOnlyList<Guid>? DocumentIds,
    IReadOnlyList<RoomGuestInput>? Guests);

public record UpdateRoomRequest(
    string? Name,
    DateOnly? ExpiryDate,
    string? Status,
    string? DownloadPolicy,
    bool? QaEnabled,
    bool? Watermark);

public record InviteGuestsRequest(IReadOnlyList<RoomGuestInput> Guests);

// ---- Read (admin) ----

public record RoomTypeDto(Guid Id, string Code, string Name, string? Description, IReadOnlyList<string> Folders);

public record RoomListItemDto(
    Guid Id, string Name, string Status, string RoomTypeCode,
    DateOnly ExpiryDate, int DocumentCount, int GuestCount, DateTime CreatedAt);

public record RoomFolderDto(Guid Id, string Name, int DisplayOrder);

public record RoomDocumentDto(Guid Id, Guid DocumentId, string FileName, string? Title, Guid FolderId, string PermissionLevel);

public record RoomGuestDto(
    Guid Id, string Email, string? Name, string Role, string PermissionLevel,
    string Status, bool NdaSigned, int Views, DateTime? LastAccessAt, string AccessToken);

public record RoomDetailDto(
    Guid Id, string Name, string? Description, string Status, string RoomTypeCode, DateOnly ExpiryDate,
    bool NdaRequired, bool Watermark, string? WatermarkText, bool QaEnabled, string DownloadPolicy,
    IReadOnlyList<RoomFolderDto> Folders, IReadOnlyList<RoomDocumentDto> Documents, IReadOnlyList<RoomGuestDto> Guests);

// ---- Q&A (admin) ----

public record RoomQuestionDto(
    Guid Id, string GuestEmail, string Category, string QuestionText,
    string Status, string? AnswerText, bool IsPublic, DateTime CreatedAt);

public record AnswerQuestionRequest(string AnswerText, bool? IsPublic);

// ---- Analytics ----

public record GuestEngagementDto(Guid GuestId, string Email, string? Name, int Views, DateTime? LastAccessAt, string Engagement);

public record RoomAnalyticsDto(Guid RoomId, int GuestCount, int TotalViews, IReadOnlyList<GuestEngagementDto> Guests);

// ---- Guest portal ----

public record GuestDocDto(Guid DocumentId, string FileName, string FileType, long FileSizeBytes);

public record GuestFolderDto(Guid Id, string Name, IReadOnlyList<GuestDocDto> Documents);

public record GuestRoomDto(
    string RoomName, string HostEntity, DateOnly ExpiryDate, bool NdaRequired, bool NdaSigned,
    bool Watermark, string? WatermarkText, string DownloadPolicy, bool QaEnabled,
    string GuestName, string GuestEmail, string PermissionLevel,
    IReadOnlyList<GuestFolderDto> Folders);

public record SignNdaRequest(string LegalName);

public record LogViewRequest(Guid DocumentId, int? Page);

public record SubmitQuestionRequest(string Category, string QuestionText);

public record GuestQuestionDto(Guid Id, string Category, string QuestionText, string Status, string? AnswerText, DateTime CreatedAt);
