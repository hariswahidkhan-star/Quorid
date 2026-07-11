using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Quorid.Infrastructure.Ai;

/// <summary>
/// Thin typed <see cref="HttpClient"/> over the Anthropic Messages API
/// (<c>POST /v1/messages</c>). Shared by every Claude-backed adapter; returns the
/// concatenated text of the response content blocks.
/// </summary>
public sealed class AnthropicChatClient
{
    private readonly HttpClient _http;
    private readonly AnthropicOptions _options;

    public AnthropicChatClient(HttpClient http, IOptions<AnthropicOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<string> CompleteAsync(
        string model, string? system, string userPrompt, int maxTokens, CancellationToken ct)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["max_tokens"] = maxTokens,
            ["messages"] = new[] { new { role = "user", content = userPrompt } },
        };
        if (!string.IsNullOrWhiteSpace(system))
            payload["system"] = system;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("anthropic-version", _options.ApiVersion);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var sb = new StringBuilder();
        if (doc.RootElement.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in content.EnumerateArray())
            {
                if (block.TryGetProperty("type", out var type) && type.GetString() == "text" &&
                    block.TryGetProperty("text", out var text))
                {
                    sb.Append(text.GetString());
                }
            }
        }
        return sb.ToString();
    }
}
