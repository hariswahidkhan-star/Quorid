using Quorid.Domain.Enums;

namespace Quorid.Application.Compliance;

/// <summary>A document viewed from the compliance engine's perspective.</summary>
public record ComplianceDoc(
    Guid Id, string? Title, string FileName, string? DomainCode, VerificationTier Tier, DateOnly? ExpiryDate);

public record RequirementInfo(Guid Id, string Name, string[] Domains, VerificationTier MinTier);

public record RequirementStatus(Guid RequirementId, string Name, string Status, Guid? DocumentId, string? DocumentTitle);

/// <summary>
/// Pure, real-time compliance evaluation: matches an entity's documents against a
/// framework's requirements and derives per-requirement status (present / expired /
/// missing) and a 0–100 score.
/// </summary>
public static class ComplianceEvaluator
{
    public static (IReadOnlyList<RequirementStatus> Requirements, int Score) Evaluate(
        IReadOnlyList<RequirementInfo> requirements, IReadOnlyList<ComplianceDoc> documents, DateOnly today)
    {
        var results = new List<RequirementStatus>();

        foreach (var req in requirements)
        {
            var candidates = documents
                .Where(d => d.DomainCode != null && req.Domains.Contains(d.DomainCode) && d.Tier >= req.MinTier)
                .ToList();

            if (candidates.Count == 0)
            {
                results.Add(new RequirementStatus(req.Id, req.Name, "missing", null, null));
                continue;
            }

            var valid = candidates.Where(d => d.ExpiryDate is null || d.ExpiryDate >= today).ToList();
            if (valid.Count > 0)
            {
                var best = valid.OrderByDescending(d => d.Tier).First();
                results.Add(new RequirementStatus(req.Id, req.Name, "present", best.Id, best.Title ?? best.FileName));
            }
            else
            {
                var latest = candidates.OrderByDescending(d => d.ExpiryDate).First();
                results.Add(new RequirementStatus(req.Id, req.Name, "expired", latest.Id, latest.Title ?? latest.FileName));
            }
        }

        var present = results.Count(r => r.Status == "present");
        var score = requirements.Count == 0 ? 0 : (int)Math.Round(100.0 * present / requirements.Count);
        return (results, score);
    }
}
