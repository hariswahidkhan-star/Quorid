using Quorid.Domain.Enums;

namespace Quorid.Application.Vault;

/// <summary>
/// Weighted 0–100 health score (spec §Module 2): document completeness (domain
/// coverage), verification-tier distribution, cross-validation pass rate, and
/// expiry status.
/// </summary>
public static class HealthScore
{
    private const int TotalDomains = 8;

    public static int Calculate(
        int domainsWithDocuments,
        int totalDocuments,
        int expiredDocuments,
        IReadOnlyList<FieldAggregate> aggregates,
        double validationPassRate)
    {
        if (totalDocuments == 0) return 0;

        var completeness = domainsWithDocuments / (double)TotalDomains;

        var tierScore = aggregates.Count == 0
            ? 0d
            : aggregates.Count(a => a.Tier != VerificationTier.T1) / (double)aggregates.Count;

        var consistency = Math.Clamp(validationPassRate, 0d, 1d);

        var expiry = 1d - Math.Min(1d, expiredDocuments / (double)totalDocuments);

        var score = 100d * (
            0.35 * completeness +
            0.25 * tierScore +
            0.30 * consistency +
            0.10 * expiry);

        return (int)Math.Round(Math.Clamp(score, 0d, 100d));
    }
}
