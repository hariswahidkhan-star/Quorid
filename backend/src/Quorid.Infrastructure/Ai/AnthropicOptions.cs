namespace Quorid.Infrastructure.Ai;

/// <summary>
/// Configuration for the Anthropic Claude adapters. When <see cref="ApiKey"/> is
/// set, the real Claude-backed services replace the offline heuristics for
/// classification, extraction, document generation, and "Ask Quorid".
/// </summary>
public class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.anthropic.com";

    public string ApiVersion { get; set; } = "2023-06-01";

    /// <summary>Fast, low-cost model for classification (spec §Module 1).</summary>
    public string ClassifyModel { get; set; } = "claude-haiku-4-5";

    /// <summary>Higher-capability model for extraction, drafting, and analytics.</summary>
    public string ReasoningModel { get; set; } = "claude-sonnet-5";

    public int MaxTokens { get; set; } = 4096;

    public bool Enabled => !string.IsNullOrWhiteSpace(ApiKey);
}
