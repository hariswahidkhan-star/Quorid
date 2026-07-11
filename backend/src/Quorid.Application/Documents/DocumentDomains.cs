namespace Quorid.Application.Documents;

/// <summary>The 8 top-level document domains (spec §Module 2 / Screen 6).</summary>
public static class DocumentDomains
{
    public record Domain(string Code, string Name);

    public static readonly IReadOnlyList<Domain> All = new[]
    {
        new Domain("IDN", "Identity"),
        new Domain("LGL", "Legal"),
        new Domain("FIN", "Financial"),
        new Domain("CMP", "Compliance"),
        new Domain("INS", "Insurance"),
        new Domain("PPL", "People"),
        new Domain("OPS", "Operations"),
        new Domain("IPR", "Intellectual Property"),
    };

    public static string NameFor(string code) =>
        All.FirstOrDefault(d => d.Code == code)?.Name ?? "Unknown";

    public static bool IsValid(string code) => All.Any(d => d.Code == code);
}
