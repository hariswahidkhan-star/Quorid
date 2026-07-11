namespace Quorid.Infrastructure.Ai;

/// <summary>
/// Helpers for pulling a JSON value out of a model response that may wrap it in
/// prose or Markdown code fences. Returns the first balanced object/array; falls
/// back to an empty literal so the caller's parse fails cleanly rather than on noise.
/// </summary>
internal static class AnthropicJson
{
    public static string ExtractObject(string text) => Extract(text, '{', '}', "{}");

    public static string ExtractArray(string text) => Extract(text, '[', ']', "[]");

    private static string Extract(string text, char open, char close, string fallback)
    {
        if (string.IsNullOrEmpty(text)) return fallback;
        var start = text.IndexOf(open);
        var end = text.LastIndexOf(close);
        return start >= 0 && end > start ? text.Substring(start, end - start + 1) : fallback;
    }
}
