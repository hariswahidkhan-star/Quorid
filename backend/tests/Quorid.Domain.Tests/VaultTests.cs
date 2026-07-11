using Quorid.Application.Vault;
using Quorid.Domain.Enums;
using Xunit;

namespace Quorid.Domain.Tests;

public class VaultAggregatorTests
{
    [Fact]
    public void Matching_value_across_two_documents_elevates_to_T2()
    {
        var d1 = Guid.NewGuid();
        var d2 = Guid.NewGuid();
        var inputs = new[]
        {
            new VaultFieldInput(d1, "IDN", "EIN", "12-3456789"),
            new VaultFieldInput(d2, "INS", "EIN", "12-3456789"),
        };

        var aggregate = Assert.Single(VaultAggregator.Aggregate(inputs));

        Assert.Equal(VerificationTier.T2, aggregate.Tier);
        Assert.False(aggregate.HasConflict);
        Assert.Equal(2, aggregate.SourceDocumentCountOrDefault());
    }

    [Fact]
    public void Conflicting_values_are_flagged_and_stay_T1()
    {
        var d1 = Guid.NewGuid();
        var d2 = Guid.NewGuid();
        var inputs = new[]
        {
            new VaultFieldInput(d1, "IDN", "EIN", "12-3456789"),
            new VaultFieldInput(d2, "FIN", "EIN", "99-9999999"),
        };

        var aggregate = Assert.Single(VaultAggregator.Aggregate(inputs));

        Assert.True(aggregate.HasConflict);
        Assert.Equal(VerificationTier.T1, aggregate.Tier);
    }
}

public class CrossValidationRulesTests
{
    [Fact]
    public void Conflicting_ein_fails_with_error_severity()
    {
        var inputs = new[]
        {
            new VaultFieldInput(Guid.NewGuid(), "IDN", "EIN", "12-3456789"),
            new VaultFieldInput(Guid.NewGuid(), "FIN", "EIN", "99-9999999"),
        };

        var results = CrossValidationRules.Run(BuildContext(inputs));
        var ein = Assert.Single(results, r => r.RuleCode == "XV-EIN-MATCH");

        Assert.False(ein.Passed);
        Assert.Equal(ValidationSeverity.Error, ein.Severity);
    }

    private static VaultContext BuildContext(VaultFieldInput[] inputs)
    {
        var aggregates = VaultAggregator.Aggregate(inputs);
        var counts = inputs
            .GroupBy(i => i.DomainCode)
            .ToDictionary(g => g.Key, g => g.Select(x => x.DocumentId).Distinct().Count());
        var totalDocs = inputs.Select(i => i.DocumentId).Distinct().Count();
        return new VaultContext(inputs, aggregates, counts, totalDocs, 0);
    }
}

public class HealthScoreTests
{
    [Fact]
    public void No_documents_scores_zero()
    {
        var score = HealthScore.Calculate(0, 0, 0, System.Array.Empty<FieldAggregate>(), 1.0);
        Assert.Equal(0, score);
    }

    [Fact]
    public void Score_is_bounded_to_0_100()
    {
        var score = HealthScore.Calculate(8, 10, 0, System.Array.Empty<FieldAggregate>(), 1.0);
        Assert.InRange(score, 0, 100);
    }
}

internal static class VaultTestExtensions
{
    public static int SourceDocumentCountOrDefault(this FieldAggregate aggregate) =>
        aggregate.DocumentIds.Count;
}
