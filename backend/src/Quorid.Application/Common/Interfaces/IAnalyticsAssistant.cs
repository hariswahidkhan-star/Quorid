using Quorid.Application.Analytics;

namespace Quorid.Application.Common.Interfaces;

/// <summary>
/// "Ask Quorid" — natural-language analytics (spec §Module 11, 42 AI touchpoints).
/// The dev implementation is a deterministic, offline heuristic that reasons over a
/// pre-computed <see cref="MetricSnapshot"/>, so the feature works without API keys.
/// A production implementation would hand the question and snapshot to Claude.
/// </summary>
public interface IAnalyticsAssistant
{
    Task<AskResult> AskAsync(string question, MetricSnapshot snapshot, CancellationToken cancellationToken = default);
}
