using System.Globalization;
using Quorid.Application.Analytics;
using Quorid.Application.Common.Interfaces;

namespace Quorid.Infrastructure.Ai;

/// <summary>
/// Offline "Ask Quorid" engine. Matches intent keywords in the question against the
/// computed <see cref="MetricSnapshot"/> and returns a data-backed, templated answer.
/// Deterministic and dependency-free — the production seam swaps in a real model.
/// </summary>
public sealed class HeuristicAnalyticsAssistant : IAnalyticsAssistant
{
    private static readonly string[] Suggestions =
    {
        "How many documents are expiring soon?",
        "What is our proposal win rate?",
        "How many active data rooms do we have?",
        "How verified is our vault?",
        "What is our compliance readiness?",
    };

    public Task<AskResult> AskAsync(string question, MetricSnapshot s, CancellationToken cancellationToken = default)
        => Task.FromResult(Answer(question ?? string.Empty, s));

    private static AskResult Answer(string question, MetricSnapshot s)
    {
        var q = question.ToLowerInvariant();

        if (Has(q, "expir", "renew", "expire"))
            return new AskResult(
                $"{s.ExpiringSoon} document{Plural(s.ExpiringSoon)} {(s.ExpiringSoon == 1 ? "is" : "are")} " +
                "expiring within the next 30 days. Review them in the Compliance calendar before they lapse.",
                "Expiring soon", s.ExpiringSoon.ToString(), Suggestions: Suggestions);

        if (Has(q, "win rate", "win", "won", "lost", "close rate"))
            return new AskResult(
                $"Your proposal win rate is {s.WinRatePercent}% across closed opportunities, with " +
                $"{Money(s.PipelineValue)} of open pipeline and {s.OpenProposals} live opportunit{(s.OpenProposals == 1 ? "y" : "ies")}.",
                "Win rate", $"{s.WinRatePercent}%", s.ProposalsByStage, Suggestions);

        if (Has(q, "pipeline", "opportunit", "deal", "proposal"))
            return new AskResult(
                $"You have {Money(s.PipelineValue)} of open proposal pipeline across {s.OpenProposals} " +
                $"opportunit{(s.OpenProposals == 1 ? "y" : "ies")}, at a {s.WinRatePercent}% win rate.",
                "Open pipeline", Money(s.PipelineValue), s.ProposalsByStage, Suggestions);

        if (Has(q, "verif", "trust", "tier", "t3", "t4"))
            return new AskResult(
                $"{s.VerifiedPercent}% of your active documents are source-verified (Tier 3+). " +
                "Raising verification tiers strengthens every compliance framework and data room at once.",
                "Verified", $"{s.VerifiedPercent}%", Suggestions: Suggestions);

        if (Has(q, "complian", "ready", "readiness", "framework", "score"))
            return s.ComplianceScore is { } score
                ? new AskResult(
                    $"Your overall compliance readiness is {score}%. Open the Compliance module for a per-framework gap analysis.",
                    "Compliance", $"{score}%", Suggestions: Suggestions)
                : new AskResult(
                    "No compliance frameworks have been scored yet. Visit the Compliance module to seed the framework catalog.",
                    "Compliance", "—", Suggestions: Suggestions);

        if (Has(q, "room", "guest", "data room"))
            return new AskResult(
                $"You have {s.ActiveRooms} active data room{Plural(s.ActiveRooms)} with {s.TotalGuests} invited " +
                $"guest{Plural(s.TotalGuests)}. Guest engagement is tracked per room in each room's analytics tab.",
                "Active rooms", s.ActiveRooms.ToString(), Suggestions: Suggestions);

        if (Has(q, "share", "external", "view"))
            return new AskResult(
                $"There {(s.ActiveShares == 1 ? "is" : "are")} {s.ActiveShares} active external share{Plural(s.ActiveShares)}, " +
                $"drawing {s.TotalShareViews} view{Plural(s.TotalShareViews)} in total.",
                "Active shares", s.ActiveShares.ToString(), Suggestions: Suggestions);

        if (Has(q, "engagement", "vendor", "client", "project"))
            return new AskResult(
                $"You have {s.ActiveEngagements} active engagement{Plural(s.ActiveEngagements)} " +
                "across projects, vendors, and clients. Track checklist progress on each engagement's page.",
                "Active engagements", s.ActiveEngagements.ToString(), Suggestions: Suggestions);

        if (Has(q, "document", "vault", "file", "how many"))
            return new AskResult(
                $"Your vault holds {s.TotalDocuments} document{Plural(s.TotalDocuments)} " +
                $"({s.ActiveDocuments} active), of which {s.VerifiedPercent}% are source-verified.",
                "Total documents", s.TotalDocuments.ToString(), s.DocumentsByStatus, Suggestions);

        // Fallback: an overview sentence plus prompts.
        return new AskResult(
            $"Here's a snapshot: {s.TotalDocuments} documents ({s.VerifiedPercent}% verified), " +
            $"{s.ActiveRooms} active rooms, {s.ActiveShares} live shares, and {Money(s.PipelineValue)} of open " +
            $"pipeline at a {s.WinRatePercent}% win rate. Ask about any of these for detail.",
            "Overview", null, Suggestions: Suggestions);
    }

    private static bool Has(string q, params string[] terms) => terms.Any(q.Contains);

    private static string Plural(int n) => n == 1 ? string.Empty : "s";

    private static string Money(decimal v) => "$" + v.ToString("N0", CultureInfo.InvariantCulture);
}
