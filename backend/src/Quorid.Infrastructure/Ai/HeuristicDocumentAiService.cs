using System.Text.RegularExpressions;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Documents;

namespace Quorid.Infrastructure.Ai;

/// <summary>
/// Offline, deterministic stand-in for the production Claude Haiku (classify) and
/// Claude Sonnet (extract) calls. It classifies from filename keywords and emits
/// the field skeleton expected for each domain so the capture-review flow works
/// without API keys. Swap for an Anthropic-backed <see cref="IDocumentAiService"/>.
/// </summary>
public class HeuristicDocumentAiService : IDocumentAiService
{
    private sealed record Rule(
        string Keyword, string Domain, string? CategoryCode, string? CategoryName,
        string? TypeCode, string? TypeName, decimal Confidence);

    private static readonly Rule[] Rules =
    {
        new("insurance", "INS", "INS-GL", "General Liability", "INS-GL-COI", "Certificate of Insurance", 91m),
        new("coi", "INS", "INS-GL", "General Liability", "INS-GL-COI", "Certificate of Insurance", 90m),
        new("policy", "INS", "INS-POL", "Policy", null, null, 84m),
        new("workers", "INS", "INS-WC", "Workers Comp", null, null, 88m),
        new("bond", "INS", "INS-BND", "Surety Bond", null, null, 89m),
        new("p&l", "FIN", "FIN-STMT", "Statements", "FIN-STMT-PL", "Profit & Loss", 90m),
        new("income", "FIN", "FIN-STMT", "Statements", "FIN-STMT-PL", "Profit & Loss", 86m),
        new("balance", "FIN", "FIN-STMT", "Statements", "FIN-STMT-BS", "Balance Sheet", 88m),
        new("tax", "FIN", "FIN-TAX", "Tax", "FIN-TAX-RET", "Tax Return", 89m),
        new("bank", "FIN", "FIN-BANK", "Banking", null, null, 82m),
        new("articles", "IDN", "IDN-FOR", "Formation", "IDN-FOR-AOI", "Articles of Incorporation", 90m),
        new("incorporation", "IDN", "IDN-FOR", "Formation", "IDN-FOR-AOI", "Articles of Incorporation", 90m),
        new("ein", "IDN", "IDN-TAX", "Tax ID", null, null, 87m),
        new("lease", "LGL", "LGL-RE", "Real Estate", "LGL-RE-LSE", "Lease Agreement", 88m),
        new("contract", "LGL", "LGL-CTR", "Contracts", null, null, 83m),
        new("agreement", "LGL", "LGL-CTR", "Contracts", null, null, 82m),
        new("osha", "CMP", "CMP-SAF", "Safety", "CMP-SAF-OSHA", "OSHA Certificate", 89m),
        new("certificate", "CMP", "CMP-CRT", "Certifications", null, null, 80m),
        new("license", "CMP", "CMP-LIC", "Licenses", null, null, 85m),
    };

    private static readonly Dictionary<string, string[]> FieldTemplates = new()
    {
        ["INS"] = new[] { "Carrier", "Policy Number", "Coverage Limit", "Effective Date", "Expiration Date", "Premium" },
        ["FIN"] = new[] { "Period", "Total Revenue", "Net Income", "Total Assets", "Total Liabilities" },
        ["IDN"] = new[] { "Legal Name", "EIN", "Formation State", "Formation Date", "Entity Type" },
        ["LGL"] = new[] { "Counterparty", "Effective Date", "Term", "Governing Law" },
        ["CMP"] = new[] { "Certificate Number", "Issuing Body", "Issue Date", "Expiration Date" },
        ["PPL"] = new[] { "Employee Name", "Role", "Start Date" },
        ["OPS"] = new[] { "Title", "Date", "Reference Number" },
        ["IPR"] = new[] { "Title", "Registration Number", "Filing Date" },
    };

    public Task<ClassificationResult> ClassifyAsync(
        string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var lower = fileName.ToLowerInvariant();
        var match = Rules.FirstOrDefault(r => lower.Contains(r.Keyword));

        var result = match is null
            ? new ClassificationResult("OPS", DocumentDomains.NameFor("OPS"), null, null, null, null, 55m)
            : new ClassificationResult(
                match.Domain, DocumentDomains.NameFor(match.Domain),
                match.CategoryCode, match.CategoryName,
                match.TypeCode, match.TypeName, match.Confidence);

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ExtractedFieldResult>> ExtractFieldsAsync(
        string fileName, ClassificationResult classification, CancellationToken cancellationToken = default)
    {
        var template = FieldTemplates.TryGetValue(classification.DomainCode, out var fields)
            ? fields
            : FieldTemplates["OPS"];

        var year = ExtractYear(fileName);

        var results = new List<ExtractedFieldResult>();
        foreach (var field in template)
        {
            string? value = null;
            decimal confidence = 48m;

            // Derive what we can from the filename; leave the rest for review.
            if (year is not null &&
                (field.Contains("Date", StringComparison.OrdinalIgnoreCase) ||
                 field.Equals("Period", StringComparison.OrdinalIgnoreCase)))
            {
                value = year;
                confidence = 90m;
            }

            results.Add(new ExtractedFieldResult(field, value, confidence, 1));
        }

        return Task.FromResult<IReadOnlyList<ExtractedFieldResult>>(results);
    }

    private static string? ExtractYear(string fileName)
    {
        var match = Regex.Match(fileName, "20\\d{2}");
        return match.Success ? match.Value : null;
    }
}
