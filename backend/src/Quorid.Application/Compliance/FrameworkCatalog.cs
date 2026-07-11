namespace Quorid.Application.Compliance;

/// <summary>The 6 pre-built compliance frameworks and their requirements (spec §Module 6).</summary>
public static class FrameworkCatalog
{
    public record RequirementSeed(string Name, string[] Domains, string MinTier);

    public record FrameworkSeed(string Code, string Name, string Description, RequirementSeed[] Requirements);

    public static readonly IReadOnlyList<FrameworkSeed> Frameworks = new[]
    {
        new FrameworkSeed("OSHA", "OSHA Safety", "Workplace safety compliance", new[]
        {
            new RequirementSeed("Workers' Compensation Insurance", new[] { "INS" }, "T2"),
            new RequirementSeed("Safety Certifications (OSHA)", new[] { "CMP" }, "T1"),
            new RequirementSeed("Incident & Injury Log", new[] { "OPS" }, "T1"),
            new RequirementSeed("Safety Training Records", new[] { "PPL", "CMP" }, "T1"),
        }),
        new FrameworkSeed("SOC2", "SOC 2", "Service Organization Control 2", new[]
        {
            new RequirementSeed("Information Security Policy", new[] { "CMP" }, "T1"),
            new RequirementSeed("Access Control Documentation", new[] { "CMP", "OPS" }, "T1"),
            new RequirementSeed("Audit / Assurance Report", new[] { "FIN", "CMP" }, "T2"),
            new RequirementSeed("Vendor Risk Assessments", new[] { "OPS" }, "T1"),
        }),
        new FrameworkSeed("PCIDSS", "PCI DSS", "Payment Card Industry Data Security Standard", new[]
        {
            new RequirementSeed("Network Security Policy", new[] { "CMP" }, "T1"),
            new RequirementSeed("Encryption & Key Management", new[] { "CMP" }, "T1"),
            new RequirementSeed("Access Control Records", new[] { "CMP", "OPS" }, "T1"),
            new RequirementSeed("Quarterly Scan Reports", new[] { "CMP" }, "T2"),
        }),
        new FrameworkSeed("HIPAA", "HIPAA", "Health Insurance Portability and Accountability Act", new[]
        {
            new RequirementSeed("Privacy Policy", new[] { "CMP" }, "T1"),
            new RequirementSeed("Business Associate Agreements", new[] { "LGL" }, "T2"),
            new RequirementSeed("Security Risk Assessment", new[] { "CMP" }, "T1"),
            new RequirementSeed("Workforce Training Records", new[] { "PPL", "CMP" }, "T1"),
        }),
        new FrameworkSeed("CONPREQ", "Construction Pre-Qualification", "Contractor pre-qualification package", new[]
        {
            new RequirementSeed("General Liability Insurance", new[] { "INS" }, "T2"),
            new RequirementSeed("Workers' Compensation Insurance", new[] { "INS" }, "T2"),
            new RequirementSeed("Surety Bond", new[] { "INS" }, "T2"),
            new RequirementSeed("OSHA Safety Certificates", new[] { "CMP" }, "T1"),
            new RequirementSeed("Audited Financial Statements", new[] { "FIN" }, "T2"),
            new RequirementSeed("Business License", new[] { "CMP", "IDN" }, "T1"),
        }),
        new FrameworkSeed("ISO27001", "ISO 27001", "Information Security Management System", new[]
        {
            new RequirementSeed("ISMS Policy", new[] { "CMP" }, "T1"),
            new RequirementSeed("Risk Assessment & Treatment", new[] { "CMP" }, "T1"),
            new RequirementSeed("Statement of Applicability", new[] { "CMP" }, "T1"),
            new RequirementSeed("Internal Audit Reports", new[] { "CMP", "FIN" }, "T2"),
        }),
    };
}
