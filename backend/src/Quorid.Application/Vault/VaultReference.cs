namespace Quorid.Application.Vault;

/// <summary>Expected key fields per domain, used to compute completion percentage.</summary>
public static class VaultReference
{
    public static readonly IReadOnlyDictionary<string, string[]> KeyFields = new Dictionary<string, string[]>
    {
        ["IDN"] = new[] { "Legal Name", "EIN", "Formation State", "Formation Date", "Entity Type" },
        ["FIN"] = new[] { "Period", "Total Revenue", "Net Income", "Total Assets", "Total Liabilities" },
        ["INS"] = new[] { "Carrier", "Policy Number", "Coverage Limit", "Effective Date", "Expiration Date" },
        ["LGL"] = new[] { "Counterparty", "Effective Date", "Term" },
        ["CMP"] = new[] { "Certificate Number", "Issuing Body", "Expiration Date" },
        ["PPL"] = new[] { "Employee Name", "Role", "Start Date" },
        ["OPS"] = new[] { "Title", "Date", "Reference Number" },
        ["IPR"] = new[] { "Title", "Registration Number", "Filing Date" },
    };

    public static int ExpectedCount(string domainCode) =>
        KeyFields.TryGetValue(domainCode, out var fields) ? fields.Length : 3;
}
