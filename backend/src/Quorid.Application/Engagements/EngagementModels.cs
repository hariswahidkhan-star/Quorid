namespace Quorid.Application.Engagements;

public record ChecklistInput(string Name, string? DomainCode, bool? IsRequired);

public record CreateEngagementRequest(
    string Type, string Name, string? ContactEmail, DateOnly? DueDate, IReadOnlyList<ChecklistInput>? Checklist);

public record UpdateEngagementRequest(string? Name, string? Status, DateOnly? DueDate);

public record AddChecklistRequest(IReadOnlyList<ChecklistInput> Items);

public record AssignDocumentRequest(Guid? DocumentId);

public record ChecklistItemDto(
    Guid Id, string Name, string? DomainCode, bool IsRequired, string Status,
    Guid? DocumentId, string? DocumentTitle, DateTime? ReceivedAt);

public record EngagementListItemDto(
    Guid Id, string Type, string Name, string? ContactEmail, string Status,
    DateOnly? DueDate, int Total, int Received, int Progress, DateTime CreatedAt);

public record EngagementDetailDto(
    Guid Id, string Type, string Name, string? ContactEmail, string Status, DateOnly? DueDate,
    string? PortalToken, int Total, int Received, int Progress, IReadOnlyList<ChecklistItemDto> Checklist);

// ---- Portal (external upload) ----

public record PortalChecklistDto(Guid Id, string Name, string Status);

public record PortalInfoDto(string EngagementName, string HostEntity, string Type, IReadOnlyList<PortalChecklistDto> Checklist);
