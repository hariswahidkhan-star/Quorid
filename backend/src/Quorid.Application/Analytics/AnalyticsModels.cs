namespace Quorid.Application.Analytics;

/// <summary>A single headline metric for the dashboard KPI row.</summary>
public record KpiDto(string Key, string Label, string Value, string? Sublabel, string Tone);

/// <summary>One slice of a categorical breakdown (a bar / donut segment).</summary>
public record BreakdownSliceDto(string Label, int Count, decimal? Value = null);

public record BreakdownDto(string Key, string Title, IReadOnlyList<BreakdownSliceDto> Slices);

public record ActivityDto(string Action, string ResourceType, DateTime At);

public record DashboardDto(
    IReadOnlyList<KpiDto> Kpis,
    IReadOnlyList<BreakdownDto> Breakdowns,
    IReadOnlyList<ActivityDto> RecentActivity);

// ---- Ask Quorid ----

public record AskRequest(string Question);

public record AskResponseDto(
    string Answer,
    string? Metric,
    string? Value,
    IReadOnlyList<BreakdownSliceDto> Breakdown,
    IReadOnlyList<string> Suggestions);

/// <summary>
/// A computed snapshot of tenant metrics handed to the analytics assistant. Keeps
/// the assistant pure and offline — it reasons over numbers, never the database.
/// </summary>
public record MetricSnapshot(
    int TotalDocuments,
    int ActiveDocuments,
    int ExpiringSoon,
    int VerifiedPercent,
    int ActiveRooms,
    int TotalGuests,
    int ActiveShares,
    int TotalShareViews,
    int ActiveEngagements,
    int OpenProposals,
    decimal PipelineValue,
    int WinRatePercent,
    int? ComplianceScore,
    IReadOnlyList<BreakdownSliceDto> DocumentsByStatus,
    IReadOnlyList<BreakdownSliceDto> ProposalsByStage);

/// <summary>Result of an assistant query, before it is mapped to the API DTO.</summary>
public record AskResult(
    string Answer,
    string? Metric = null,
    string? Value = null,
    IReadOnlyList<BreakdownSliceDto>? Breakdown = null,
    IReadOnlyList<string>? Suggestions = null);

// ---- Reports ----

public record ReportSummaryDto(string Key, string Title, string Category, string Description);

public record ReportResultDto(
    string Key,
    string Title,
    string Category,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int TotalRows);
