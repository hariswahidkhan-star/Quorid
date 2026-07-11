namespace Quorid.Application.Vault;

/// <summary>A field aggregated across an entity's documents, with its tier.</summary>
public record VaultFieldDto(
    string FieldName,
    string? Value,
    string Tier,
    int SourceDocumentCount,
    IReadOnlyList<Guid> SourceDocumentIds,
    bool HasConflict);

/// <summary>One of the 8 domain cards on the Identity Vault screen (D-03).</summary>
public record DomainCardDto(
    string DomainCode,
    string DomainName,
    int DocumentCount,
    int FieldCount,
    int CompletionPercent,
    IReadOnlyList<VaultFieldDto> Fields);

public record TierBreakdownDto(int T1, int T2, int T3, int T4);

public record VaultProfileDto(
    Guid EntityId,
    int HealthScore,
    int DocumentCount,
    TierBreakdownDto Tiers,
    IReadOnlyList<DomainCardDto> Domains);

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public record CrossValidationResultDto(
    string RuleCode,
    string RuleName,
    ValidationSeverity Severity,
    bool Passed,
    string Message);

public record CrossValidationReportDto(
    Guid EntityId,
    int Total,
    int Passed,
    int Failed,
    int PassRatePercent,
    IReadOnlyList<CrossValidationResultDto> Results);
