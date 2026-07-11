using Quorid.Domain.Enums;

namespace Quorid.Application.Vault;

/// <summary>One extracted field, tagged with its source document's domain.</summary>
public record VaultFieldInput(Guid DocumentId, string DomainCode, string FieldName, string? Value);

/// <summary>A field aggregated across documents, with a derived verification tier.</summary>
public record FieldAggregate(
    string FieldName,
    string? PrimaryValue,
    IReadOnlyList<string> DistinctValues,
    IReadOnlyList<Guid> DocumentIds,
    VerificationTier Tier,
    bool HasConflict);

/// <summary>Everything the cross-validation rules and health score need.</summary>
public record VaultContext(
    IReadOnlyList<VaultFieldInput> Fields,
    IReadOnlyList<FieldAggregate> Aggregates,
    IReadOnlyDictionary<string, int> DomainDocumentCounts,
    int TotalDocuments,
    int ExpiredDocuments);
