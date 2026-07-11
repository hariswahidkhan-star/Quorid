using System.Text;
using System.Text.RegularExpressions;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.DocumentCreation;

namespace Quorid.Infrastructure.Ai;

/// <summary>
/// Offline, deterministic document composer standing in for Claude Sonnet
/// (Module 4). It fills template placeholders from the supplied token map, expands
/// the free-text prompt into the body, and can polish an existing draft under a
/// plain-language instruction. Swap for an Anthropic-backed <see cref="IDocumentGenerator"/>.
/// </summary>
public sealed class HeuristicDocumentGenerator : IDocumentGenerator
{
    private static readonly Regex TokenPattern = new(@"\{\{\s*([\w.]+)\s*\}\}", RegexOptions.Compiled);
    private static readonly Regex HorizontalWs = new(@"[ \t]+", RegexOptions.Compiled);

    public Task<string> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(Compose(request));

    public Task<string> ImproveAsync(string body, string instruction, CancellationToken cancellationToken = default)
        => Task.FromResult(Polish(body ?? string.Empty, instruction ?? string.Empty));

    // ---- Generate ----

    private static string Compose(GenerationRequest request)
    {
        var narrative = Narrative(request.Prompt);

        if (!string.IsNullOrWhiteSpace(request.TemplateBody))
            return FillTokens(request.TemplateBody!, request.Tokens, narrative);

        // No template: compose a clean, structured document from scratch.
        var entity = request.Tokens.TryGetValue("entity.name", out var name) && !string.IsNullOrWhiteSpace(name)
            ? name : "our organization";
        var recipient = request.Tokens.TryGetValue("recipient", out var r) && !string.IsNullOrWhiteSpace(r)
            ? r : null;
        var date = request.Tokens.TryGetValue("date", out var d) && !string.IsNullOrWhiteSpace(d) ? d : Today();

        var sb = new StringBuilder();
        sb.Append(request.DocumentType.ToUpperInvariant()).Append("\n\n");
        sb.Append(date).Append("\n\n");
        if (recipient is not null) sb.Append("Dear ").Append(recipient).Append(",\n\n");
        sb.Append(narrative).Append("\n\n");
        sb.Append("Sincerely,\n").Append(entity);
        return sb.ToString();
    }

    private static string FillTokens(string template, IReadOnlyDictionary<string, string> tokens, string narrative)
    {
        return TokenPattern.Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim().ToLowerInvariant();
            if (key is "body" or "content") return narrative;
            if (tokens.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
            if (key == "date") return Today();
            // Unknown / empty token: a readable placeholder rather than a raw {{token}}.
            var label = key.Contains('.') ? key[(key.IndexOf('.') + 1)..] : key;
            return "[" + label.Replace('_', ' ') + "]";
        });
    }

    private static string Narrative(string prompt)
    {
        var p = (prompt ?? string.Empty).Trim();
        if (p.Length == 0) return "[Describe the key details here.]";
        p = char.ToUpperInvariant(p[0]) + p[1..];
        if (!EndsWithTerminal(p)) p += ".";
        return p;
    }

    // ---- Improve ----

    private static string Polish(string body, string instruction)
    {
        var lower = instruction.ToLowerInvariant();

        var paragraphs = body
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanParagraph)
            .Where(s => s.Length > 0)
            .ToList();

        if (paragraphs.Count == 0) return body;

        var wantsShorter = Mentions(lower, "concise", "short", "shorten", "tighten", "brief");
        var wantsFormal = Mentions(lower, "formal", "professional", "polish");

        if (wantsShorter && paragraphs.Count > 3)
        {
            // Keep the opening, the core, and the closing.
            paragraphs = new List<string> { paragraphs[0], paragraphs[1], paragraphs[^1] };
        }

        if (wantsFormal && !EndsWithClosing(paragraphs[^1]))
            paragraphs.Add("We appreciate your time and consideration.");

        return string.Join("\n\n", paragraphs);
    }

    private static string CleanParagraph(string paragraph)
    {
        // Collapse runs of spaces/tabs per line, but keep the block's internal
        // line breaks so structured headers (SOW, capability statement) survive.
        var lines = paragraph.Replace("\r", string.Empty).Split('\n')
            .Select(l => HorizontalWs.Replace(l.Trim(), " "));
        var text = string.Join("\n", lines).Trim();
        if (text.Length == 0) return text;
        text = char.ToUpperInvariant(text[0]) + text[1..];
        if (!EndsWithTerminal(text) && !text.EndsWith(",")) text += ".";
        return text;
    }

    // ---- helpers ----

    private static bool Mentions(string haystack, params string[] needles) => needles.Any(haystack.Contains);

    private static bool EndsWithTerminal(string s) =>
        s.EndsWith(".") || s.EndsWith("!") || s.EndsWith("?") || s.EndsWith(":");

    private static bool EndsWithClosing(string s)
    {
        var t = s.ToLowerInvariant();
        return t.Contains("sincerely") || t.Contains("regards") || t.Contains("consideration") ||
               t.Contains("respectfully");
    }

    private static string Today() => DateTime.UtcNow.ToString("MMMM d, yyyy");
}
