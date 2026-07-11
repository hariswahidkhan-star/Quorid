namespace Quorid.Application.Vault;

/// <summary>
/// The cross-validation engine (spec §Module 2). A representative set of the
/// 18 rules that run after every capture — EIN/name/revenue consistency, date
/// logic, and coverage/expiry checks. Rules are pure functions over
/// <see cref="VaultContext"/>; a null result means the rule does not apply.
/// </summary>
public static class CrossValidationRules
{
    public static IReadOnlyList<CrossValidationResultDto> Run(VaultContext context)
    {
        var candidates = new[]
        {
            EinConsistency(context),
            LegalNameConsistency(context),
            RevenueConsistency(context),
            IdentityDocumentPresent(context),
            FormationDatePresent(context),
            FormationPrecedesContracts(context),
            InsuranceExpiration(context),
            InsuranceCoveragePresent(context),
        };

        return candidates.Where(r => r is not null).Select(r => r!).ToList();
    }

    private static CrossValidationResultDto? EinConsistency(VaultContext ctx)
    {
        var ein = Field(ctx, "EIN");
        if (ein is null || ein.DistinctValues.Count == 0) return null;

        return new CrossValidationResultDto(
            "XV-EIN-MATCH", "EIN consistency", ValidationSeverity.Error, !ein.HasConflict,
            ein.HasConflict
                ? $"EIN differs across documents: {string.Join(" / ", ein.DistinctValues)}."
                : "EIN is consistent across all documents.");
    }

    private static CrossValidationResultDto? LegalNameConsistency(VaultContext ctx)
    {
        var name = Field(ctx, "Legal Name");
        if (name is null || name.DistinctValues.Count == 0) return null;

        return new CrossValidationResultDto(
            "XV-NAME-MATCH", "Legal name consistency", ValidationSeverity.Warning, !name.HasConflict,
            name.HasConflict
                ? $"Legal name differs across documents: {string.Join(" / ", name.DistinctValues)}."
                : "Legal name is consistent across documents.");
    }

    private static CrossValidationResultDto? RevenueConsistency(VaultContext ctx)
    {
        var revenue = Field(ctx, "Total Revenue");
        if (revenue is null || revenue.DistinctValues.Count == 0) return null;

        return new CrossValidationResultDto(
            "XV-REV-MATCH", "Revenue consistency", ValidationSeverity.Warning, !revenue.HasConflict,
            revenue.HasConflict
                ? "Revenue figures are inconsistent across financial documents."
                : "Revenue figures are consistent.");
    }

    private static CrossValidationResultDto IdentityDocumentPresent(VaultContext ctx)
    {
        var present = DocCount(ctx, "IDN") > 0;
        return new CrossValidationResultDto(
            "XV-IDN-DOC", "Identity document on file", ValidationSeverity.Warning, present,
            present ? "At least one identity document is on file." : "No identity document is on file.");
    }

    private static CrossValidationResultDto? FormationDatePresent(VaultContext ctx)
    {
        if (DocCount(ctx, "IDN") == 0) return null;

        var present = Field(ctx, "Formation Date")?.PrimaryValue is not null;
        return new CrossValidationResultDto(
            "XV-FORM-DATE", "Formation date present", ValidationSeverity.Warning, present,
            present ? "Formation date is recorded." : "Formation date is missing from identity documents.");
    }

    private static CrossValidationResultDto? FormationPrecedesContracts(VaultContext ctx)
    {
        var formation = Field(ctx, "Formation Date")?.PrimaryValue;
        var effective = Field(ctx, "Effective Date")?.PrimaryValue;
        if (formation is null || effective is null) return null;
        if (!DateTime.TryParse(formation, out var f) || !DateTime.TryParse(effective, out var e)) return null;

        var passed = f <= e;
        return new CrossValidationResultDto(
            "XV-FORM-BEFORE-CONTRACT", "Formation precedes contracts", ValidationSeverity.Error, passed,
            passed
                ? "Formation date precedes contract effective dates."
                : "Formation date is later than a contract effective date.");
    }

    private static CrossValidationResultDto? InsuranceExpiration(VaultContext ctx)
    {
        if (DocCount(ctx, "INS") == 0) return null;

        var expiry = Field(ctx, "Expiration Date")?.PrimaryValue;
        if (expiry is null)
        {
            return new CrossValidationResultDto(
                "XV-INS-EXPIRY", "Insurance expiration present", ValidationSeverity.Warning, false,
                "Insurance expiration date is missing.");
        }

        if (DateTime.TryParse(expiry, out var date) && date < DateTime.UtcNow)
        {
            return new CrossValidationResultDto(
                "XV-INS-EXPIRY", "Insurance expiration present", ValidationSeverity.Error, false,
                $"Insurance appears to have expired ({expiry}).");
        }

        return new CrossValidationResultDto(
            "XV-INS-EXPIRY", "Insurance expiration present", ValidationSeverity.Info, true,
            "Insurance has a valid expiration date on file.");
    }

    private static CrossValidationResultDto? InsuranceCoveragePresent(VaultContext ctx)
    {
        if (DocCount(ctx, "INS") == 0) return null;

        var present = Field(ctx, "Coverage Limit")?.PrimaryValue is not null;
        return new CrossValidationResultDto(
            "XV-INS-COVERAGE", "Insurance coverage present", ValidationSeverity.Warning, present,
            present ? "Coverage limit is recorded." : "Coverage limit is missing from insurance documents.");
    }

    private static FieldAggregate? Field(VaultContext ctx, string name) =>
        ctx.Aggregates.FirstOrDefault(a => a.FieldName.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static int DocCount(VaultContext ctx, string domainCode) =>
        ctx.DomainDocumentCounts.TryGetValue(domainCode, out var count) ? count : 0;
}
