using System.Text;
using Microsoft.Extensions.Options;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.DocumentCreation;

namespace Quorid.Infrastructure.Ai;

/// <summary>
/// Claude Sonnet-backed <see cref="IDocumentGenerator"/> (spec §Module 4). Replaces
/// <see cref="HeuristicDocumentGenerator"/> when an Anthropic API key is configured.
/// Falls back to the offline composer if the model call fails, so drafting never
/// hard-errors.
/// </summary>
public sealed class AnthropicDocumentGenerator : IDocumentGenerator
{
    private static readonly HeuristicDocumentGenerator Fallback = new();

    private readonly AnthropicChatClient _client;
    private readonly AnthropicOptions _options;

    public AnthropicDocumentGenerator(AnthropicChatClient client, IOptions<AnthropicOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<string> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default)
    {
        const string system =
            "You are a professional business-document writer for the Quorid platform. " +
            "Produce a clean, ready-to-send document. Fill any {{token}} placeholders from the provided " +
            "context values. Return ONLY the finished document text — no preamble, no explanation, no Markdown fences.";

        var sb = new StringBuilder();
        sb.Append("Document type: ").Append(request.DocumentType).Append("\n\n");
        if (request.Tokens.Count > 0)
        {
            sb.Append("Context values:\n");
            foreach (var (key, value) in request.Tokens)
                sb.Append("- ").Append(key).Append(": ").Append(value).Append('\n');
            sb.Append('\n');
        }
        if (!string.IsNullOrWhiteSpace(request.TemplateBody))
            sb.Append("Template (fill the {{token}} placeholders; {{body}} is where the instruction is expanded):\n")
              .Append(request.TemplateBody).Append("\n\n");
        sb.Append("Instruction / key points:\n").Append(request.Prompt);

        try
        {
            var text = await _client.CompleteAsync(_options.ReasoningModel, system, sb.ToString(), _options.MaxTokens, cancellationToken);
            return string.IsNullOrWhiteSpace(text) ? await Fallback.GenerateAsync(request, cancellationToken) : text.Trim();
        }
        catch
        {
            return await Fallback.GenerateAsync(request, cancellationToken);
        }
    }

    public async Task<string> ImproveAsync(string body, string instruction, CancellationToken cancellationToken = default)
    {
        const string system =
            "You revise business documents. Apply the user's instruction and return ONLY the revised " +
            "document text — no preamble, no commentary, no Markdown fences.";

        var prompt = $"Instruction: {instruction}\n\nDocument:\n{body}";

        try
        {
            var text = await _client.CompleteAsync(_options.ReasoningModel, system, prompt, _options.MaxTokens, cancellationToken);
            return string.IsNullOrWhiteSpace(text) ? await Fallback.ImproveAsync(body, instruction, cancellationToken) : text.Trim();
        }
        catch
        {
            return await Fallback.ImproveAsync(body, instruction, cancellationToken);
        }
    }
}
