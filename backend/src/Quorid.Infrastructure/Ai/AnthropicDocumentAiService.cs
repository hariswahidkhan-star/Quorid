using System.Text.Json;
using Microsoft.Extensions.Options;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Documents;

namespace Quorid.Infrastructure.Ai;

/// <summary>
/// Claude-backed <see cref="IDocumentAiService"/> (spec §Module 1): Haiku classifies,
/// Sonnet extracts. Replaces <see cref="HeuristicDocumentAiService"/> when an
/// Anthropic API key is configured. Model output is parsed defensively — any
/// malformed response degrades to a low-confidence result rather than throwing.
/// </summary>
public sealed class AnthropicDocumentAiService : IDocumentAiService
{
    private readonly AnthropicChatClient _client;
    private readonly AnthropicOptions _options;

    public AnthropicDocumentAiService(AnthropicChatClient client, IOptions<AnthropicOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<ClassificationResult> ClassifyAsync(
        string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        const string system =
            "You classify business documents into a Domain > Category > Type taxonomy. " +
            "Domains: INS (Insurance), FIN (Financial), IDN (Identity/Formation), LGL (Legal), " +
            "CMP (Compliance/Certifications), HR (Human Resources), OPS (Operations), OTH (Other). " +
            "Respond with ONLY a JSON object: " +
            "{\"domainCode\":\"\",\"domainName\":\"\",\"categoryCode\":null,\"categoryName\":null," +
            "\"typeCode\":null,\"typeName\":null,\"confidence\":0}. Confidence is 0-100.";

        var prompt = $"File name: {fileName}\nContent type: {contentType}\nClassify this document.";

        try
        {
            var text = await _client.CompleteAsync(_options.ClassifyModel, system, prompt, 512, cancellationToken);
            using var doc = JsonDocument.Parse(AnthropicJson.ExtractObject(text));
            var root = doc.RootElement;

            return new ClassificationResult(
                Str(root, "domainCode") ?? "OTH",
                Str(root, "domainName") ?? "Other",
                Str(root, "categoryCode"),
                Str(root, "categoryName"),
                Str(root, "typeCode"),
                Str(root, "typeName"),
                Num(root, "confidence"));
        }
        catch
        {
            // Model unavailable or unparseable — flag for manual review.
            return new ClassificationResult("OTH", "Other", null, null, null, null, 0m);
        }
    }

    public async Task<IReadOnlyList<ExtractedFieldResult>> ExtractFieldsAsync(
        string fileName, ClassificationResult classification, CancellationToken cancellationToken = default)
    {
        const string system =
            "You extract the key structured fields from a business document. " +
            "Respond with ONLY a JSON array of objects: " +
            "[{\"fieldName\":\"\",\"fieldValue\":\"\",\"confidence\":0}]. Confidence is 0-100. " +
            "Return an empty array if no fields are identifiable.";

        var prompt =
            $"File name: {fileName}\n" +
            $"Classified as: {classification.DomainName} / {classification.CategoryName} / {classification.TypeName}\n" +
            "List the fields a reviewer would expect for this document type.";

        try
        {
            var text = await _client.CompleteAsync(_options.ReasoningModel, system, prompt, 1024, cancellationToken);
            using var doc = JsonDocument.Parse(AnthropicJson.ExtractArray(text));

            var results = new List<ExtractedFieldResult>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var name = Str(item, "fieldName");
                if (string.IsNullOrWhiteSpace(name)) continue;
                results.Add(new ExtractedFieldResult(name!, Str(item, "fieldValue"), Num(item, "confidence"), null));
            }
            return results;
        }
        catch
        {
            return Array.Empty<ExtractedFieldResult>();
        }
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)
            ? Math.Clamp(d, 0m, 100m)
            : 0m;
}
