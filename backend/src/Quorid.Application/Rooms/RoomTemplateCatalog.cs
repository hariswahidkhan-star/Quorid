namespace Quorid.Application.Rooms;

/// <summary>The 6 system room types and their default folder structures (spec §3.1).</summary>
public static class RoomTemplateCatalog
{
    public record RoomTypeSeed(string Code, string Name, string Description, string[] Folders);

    public static readonly IReadOnlyList<RoomTypeSeed> Types = new[]
    {
        new RoomTypeSeed("DR-INV", "Investor Due Diligence", "Fundraising / M&A sell-side",
            new[] { "Corporate", "Cap Table", "Financial", "Legal", "IP", "Team", "Compliance", "Metrics" }),
        new RoomTypeSeed("DR-AUD", "Annual Audit", "Annual financial audit",
            new[] { "Financial", "GL", "Bank Recs", "Revenue", "AP/AR", "Fixed Assets", "Controls", "Board Minutes" }),
        new RoomTypeSeed("DR-TAX", "Tax Preparation", "CPA tax preparation",
            new[] { "Federal", "State", "Payroll", "Supporting", "Prior Years", "Correspondence" }),
        new RoomTypeSeed("DR-BNK", "Banking / Loan", "Loan applications",
            new[] { "Application", "Corporate", "Financial", "Tax", "Collateral", "Insurance", "Existing Debt", "AR/AP" }),
        new RoomTypeSeed("DR-LEG", "Legal Discovery", "Litigation discovery",
            new[] { "Privileged", "Responsive", "Non-Responsive", "Hot Docs", "Correspondence", "Depositions", "Expert", "Production" }),
        new RoomTypeSeed("DR-CMP", "Compliance", "Regulatory examination",
            new[] { "Licenses", "Insurance", "Financial", "Procedures", "Training", "Incidents", "Corrective", "Prior Audits" }),
    };

    public static RoomTypeSeed? ByCode(string code) => Types.FirstOrDefault(t => t.Code == code);
}
