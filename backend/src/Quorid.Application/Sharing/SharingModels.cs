namespace Quorid.Application.Sharing;

public record CreateShareRequest(
    string RecipientEmail,
    string? RecipientName,
    string PermissionLevel,
    DateOnly? ExpiryDate,
    bool? Watermark,
    bool? TrackViews,
    string? Message);

public record ShareDto(
    Guid Id,
    Guid DocumentId,
    string? DocumentTitle,
    string RecipientEmail,
    string? RecipientName,
    string PermissionLevel,
    string Status,
    DateOnly? ExpiryDate,
    bool Watermark,
    bool TrackViews,
    int ViewCount,
    DateTime? LastViewedAt,
    string AccessToken,
    DateTime CreatedAt);

public record ShareViewDto(string IpAddress, string? UserAgent, DateTime ViewedAt);

public record ShareAnalyticsDto(
    Guid ShareId,
    int ViewCount,
    DateTime? LastViewedAt,
    IReadOnlyList<ShareViewDto> Views);

/// <summary>What an external recipient sees at the tokenized share link.</summary>
public record PublicShareDto(
    string DocumentTitle,
    string FileType,
    string PermissionLevel,
    bool Watermark,
    string Message);
