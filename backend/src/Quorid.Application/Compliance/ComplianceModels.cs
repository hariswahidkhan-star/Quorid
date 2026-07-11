namespace Quorid.Application.Compliance;

public record FrameworkDto(Guid Id, string Code, string Name, string? Description, bool IsSystem, int RequirementCount);

public record RequirementStatusDto(Guid RequirementId, string Name, string Status, Guid? DocumentId, string? DocumentTitle);

public record FrameworkStatusDto(
    Guid FrameworkId, string Code, string Name, int Score, int Present, int Total,
    IReadOnlyList<RequirementStatusDto> Requirements);

public record FrameworkOverviewDto(Guid FrameworkId, string Code, string Name, int Score, int Present, int Total);

public record ComplianceOverviewDto(Guid EntityId, int OverallScore, IReadOnlyList<FrameworkOverviewDto> Frameworks);

public record CreateRequirementInput(string Name, IReadOnlyList<string>? Domains, string? MinTier);

public record CreateFrameworkRequest(string Name, string? Description, IReadOnlyList<CreateRequirementInput>? Requirements);

public record ComplianceCalendarItemDto(
    string FrameworkName, string RequirementName, Guid DocumentId, string DocumentTitle,
    DateOnly ExpiryDate, int DaysUntilExpiry);
