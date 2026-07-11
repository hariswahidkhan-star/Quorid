using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Quorid.Application.Analytics;
using Quorid.Application.Common.Interfaces;

namespace Quorid.Infrastructure.Ai;

/// <summary>
/// Claude Sonnet-backed "Ask Quorid" (<see cref="IAnalyticsAssistant"/>, spec
/// §Module 11). The endpoint still computes the <see cref="MetricSnapshot"/>; this
/// adapter reasons over those numbers only — the model never sees the database.
/// Falls back to the offline heuristic on any error.
/// </summary>
public sealed class AnthropicAnalyticsAssistant : IAnalyticsAssistant
{
    private static readonly HeuristicAnalyticsAssistant Fallback = new();

    private static readonly string[] Suggestions =
    {
        "How many documents are expiring soon?",
        "What is our proposal win rate?",
        "How many active data rooms do we have?",
        "How verified is our vault?",
        "What is our compliance readiness?",
    };

    private readonly AnthropicChatClient _client;
    private readonly AnthropicOptions _options;

    public AnthropicAnalyticsAssistant(AnthropicChatClient client, IOptions<AnthropicOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<AskResult> AskAsync(string question, MetricSnapshot s, CancellationToken cancellationToken = default)
    {
        const string system =
            "You are Quorid's analytics assistant. Answer the user's question in 1-3 sentences using ONLY " +
            "the metrics provided. Do not invent numbers. If the metrics don't cover the question, say so briefly.";

        var metrics = new StringBuilder()
            .Append("Total documents: ").Append(s.TotalDocuments).Append(" (active ").Append(s.ActiveDocuments).Append(")\n")
            .Append("Verified (Tier 3+): ").Append(s.VerifiedPercent).Append("%\n")
            .Append("Expiring within 30 days: ").Append(s.ExpiringSoon).Append('\n')
            .Append("Compliance readiness: ").Append(s.ComplianceScore is { } c ? c + "%" : "not scored").Append('\n')
            .Append("Active data rooms: ").Append(s.ActiveRooms).Append(", invited guests: ").Append(s.TotalGuests).Append('\n')
            .Append("Active external shares: ").Append(s.ActiveShares).Append(", total views: ").Append(s.TotalShareViews).Append('\n')
            .Append("Active engagements: ").Append(s.ActiveEngagements).Append('\n')
            .Append("Open proposals: ").Append(s.OpenProposals)
            .Append(", pipeline value: $").Append(s.PipelineValue.ToString("N0", CultureInfo.InvariantCulture))
            .Append(", win rate: ").Append(s.WinRatePercent).Append("%\n")
            .ToString();

        var prompt = $"Metrics:\n{metrics}\nQuestion: {question}";

        try
        {
            var answer = await _client.CompleteAsync(_options.ReasoningModel, system, prompt, 512, cancellationToken);
            if (string.IsNullOrWhiteSpace(answer))
                return await Fallback.AskAsync(question, s, cancellationToken);

            return new AskResult(answer.Trim(), Suggestions: Suggestions);
        }
        catch
        {
            return await Fallback.AskAsync(question, s, cancellationToken);
        }
    }
}
