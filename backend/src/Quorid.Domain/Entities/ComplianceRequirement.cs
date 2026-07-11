using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// A single requirement within a framework, satisfied by documents of the given
/// domains meeting a minimum verification tier.
/// </summary>
public class ComplianceRequirement : BaseEntity
{
    public Guid FrameworkId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? RegulationRef { get; set; }

    /// <summary>JSON array of accepted document domain codes, e.g. ["INS","CMP"].</summary>
    public string RequiredDocTypes { get; set; } = "[]";

    public VerificationTier MinVerificationTier { get; set; } = VerificationTier.T1;

    public string Status { get; set; } = "active";
}
