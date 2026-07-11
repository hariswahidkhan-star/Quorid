using Quorid.Domain.Enums;

namespace Quorid.Application.Vault;

/// <summary>
/// Aggregates extracted fields into canonical vault fields and derives their
/// verification tier: a consistent value seen across 2+ documents is elevated to
/// T2 (Cross-Referenced); conflicting values are flagged. T3/T4 require external
/// / counter-party verification (later phases).
/// </summary>
public static class VaultAggregator
{
    public static IReadOnlyList<FieldAggregate> Aggregate(IEnumerable<VaultFieldInput> inputs)
    {
        return inputs
            .Where(i => !string.IsNullOrWhiteSpace(i.FieldName))
            .GroupBy(i => i.FieldName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var withValues = group
                    .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                    .ToList();

                var distinctValues = withValues
                    .Select(x => x.Value!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var documentIds = group.Select(x => x.DocumentId).Distinct().ToList();
                var documentsWithValue = withValues.Select(x => x.DocumentId).Distinct().Count();

                var hasConflict = distinctValues.Count > 1;
                var tier = distinctValues.Count == 1 && documentsWithValue >= 2
                    ? VerificationTier.T2
                    : VerificationTier.T1;

                var primaryValue = distinctValues.Count > 0 ? distinctValues[0] : null;

                return new FieldAggregate(group.Key, primaryValue, distinctValues, documentIds, tier, hasConflict);
            })
            .OrderBy(a => a.FieldName)
            .ToList();
    }
}
