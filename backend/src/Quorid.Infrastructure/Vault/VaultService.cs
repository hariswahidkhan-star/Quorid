using Microsoft.EntityFrameworkCore;
using Quorid.Application.Documents;
using Quorid.Application.Vault;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Infrastructure.Vault;

/// <summary>
/// Builds the Identity Vault view over an entity's documents and extracted
/// fields, then runs the pure aggregation, cross-validation, and health-score
/// logic from the Application layer.
/// </summary>
public class VaultService : IVaultService
{
    private readonly ApplicationDbContext _db;

    public VaultService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<VaultProfileDto?> GetProfileAsync(Guid entityId, CancellationToken cancellationToken = default)
    {
        var built = await BuildAsync(entityId, cancellationToken);
        if (built is null) return null;

        var (context, inputs, domainDocCounts, totalDocs, expiredDocs) = built.Value;

        var validation = CrossValidationRules.Run(context);
        var passRate = validation.Count == 0 ? 1d : validation.Count(r => r.Passed) / (double)validation.Count;

        var domainsWithDocs = domainDocCounts.Count(kv => kv.Value > 0);
        var health = HealthScore.Calculate(domainsWithDocs, totalDocs, expiredDocs, context.Aggregates, passRate);

        var cards = new List<DomainCardDto>();
        foreach (var domain in DocumentDomains.All)
        {
            var domainInputs = inputs.Where(i => i.DomainCode == domain.Code).ToList();
            var domainAggregates = VaultAggregator.Aggregate(domainInputs);

            var docCount = domainDocCounts.TryGetValue(domain.Code, out var dc) ? dc : 0;
            var expected = VaultReference.ExpectedCount(domain.Code);
            var withValue = domainAggregates.Count(a => a.PrimaryValue is not null);
            var completion = expected == 0 ? 0 : (int)Math.Round(100.0 * Math.Min(withValue, expected) / expected);

            cards.Add(new DomainCardDto(
                domain.Code, domain.Name, docCount, domainAggregates.Count, completion,
                domainAggregates.Select(ToFieldDto).ToList()));
        }

        var tiers = new TierBreakdownDto(
            context.Aggregates.Count(a => a.Tier == VerificationTier.T1),
            context.Aggregates.Count(a => a.Tier == VerificationTier.T2),
            context.Aggregates.Count(a => a.Tier == VerificationTier.T3),
            context.Aggregates.Count(a => a.Tier == VerificationTier.T4));

        return new VaultProfileDto(entityId, health, totalDocs, tiers, cards);
    }

    public async Task<CrossValidationReportDto?> CrossValidateAsync(Guid entityId, CancellationToken cancellationToken = default)
    {
        var built = await BuildAsync(entityId, cancellationToken);
        if (built is null) return null;

        var results = CrossValidationRules.Run(built.Value.Context);
        var passed = results.Count(r => r.Passed);
        var failed = results.Count - passed;
        var passRate = results.Count == 0 ? 100 : (int)Math.Round(100.0 * passed / results.Count);

        return new CrossValidationReportDto(entityId, results.Count, passed, failed, passRate, results);
    }

    private async Task<BuildResult?> BuildAsync(Guid entityId, CancellationToken ct)
    {
        var entityExists = await _db.Entities.AnyAsync(e => e.Id == entityId, ct);
        if (!entityExists) return null;

        var docs = await _db.Documents.AsNoTracking()
            .Where(d => d.EntityId == entityId && d.Status != DocumentStatus.Archived)
            .Select(d => new { d.Id, d.DomainCode, d.ExpiryDate })
            .ToListAsync(ct);

        var docIds = docs.Select(d => d.Id).ToList();
        var docDomain = docs.ToDictionary(
            d => d.Id, d => string.IsNullOrEmpty(d.DomainCode) ? "OPS" : d.DomainCode);

        var rawFields = docIds.Count == 0
            ? new List<RawFieldRow>()
            : (await _db.ExtractedFields.AsNoTracking()
                    .Where(f => docIds.Contains(f.DocumentId))
                    .Select(f => new { f.DocumentId, f.FieldName, Value = f.OverrideValue ?? f.FieldValue })
                    .ToListAsync(ct))
                .Select(f => new RawFieldRow(f.DocumentId, f.FieldName, f.Value))
                .ToList();

        var inputs = rawFields
            .Select(f => new VaultFieldInput(
                f.DocumentId,
                docDomain.TryGetValue(f.DocumentId, out var domain) ? domain : "OPS",
                f.FieldName,
                f.Value))
            .ToList();

        var aggregates = VaultAggregator.Aggregate(inputs);

        var domainDocCounts = docs
            .GroupBy(d => string.IsNullOrEmpty(d.DomainCode) ? "OPS" : d.DomainCode)
            .ToDictionary(g => g.Key, g => g.Count());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expired = docs.Count(d => d.ExpiryDate is { } e && e < today);

        var context = new VaultContext(inputs, aggregates, domainDocCounts, docs.Count, expired);
        return new BuildResult(context, inputs, domainDocCounts, docs.Count, expired);
    }

    private static VaultFieldDto ToFieldDto(FieldAggregate a) =>
        new(a.FieldName, a.PrimaryValue, a.Tier.ToString(), a.DocumentIds.Count, a.DocumentIds, a.HasConflict);

    private record RawFieldRow(Guid DocumentId, string FieldName, string? Value);

    private readonly record struct BuildResult(
        VaultContext Context,
        IReadOnlyList<VaultFieldInput> Inputs,
        IReadOnlyDictionary<string, int> DomainDocumentCounts,
        int TotalDocuments,
        int ExpiredDocuments);
}
