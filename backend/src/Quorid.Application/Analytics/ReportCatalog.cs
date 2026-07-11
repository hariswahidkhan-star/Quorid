namespace Quorid.Application.Analytics;

/// <summary>
/// The 18 pre-built reports (spec §Module 11). Each entry is metadata only; the
/// actual aggregation lives in the analytics endpoint, keyed by <see cref="ReportDefinition.Key"/>.
/// </summary>
public static class ReportCatalog
{
    public static readonly IReadOnlyList<ReportDefinition> Reports = new List<ReportDefinition>
    {
        // Documents
        new("documents-by-status", "Documents by Status", "Documents",
            "Vault inventory grouped by lifecycle status."),
        new("documents-by-tier", "Documents by Verification Tier", "Documents",
            "Trust distribution across the four verification tiers."),
        new("documents-by-domain", "Documents by Domain", "Documents",
            "Coverage across document domains (legal, financial, insurance, …)."),
        new("documents-expiring", "Documents Expiring Soon", "Documents",
            "Documents with an expiry date inside the next 90 days."),
        new("documents-by-privacy", "Documents by Privacy Level", "Documents",
            "How the vault is distributed across the four privacy levels."),
        new("documents-by-entity", "Document Inventory by Entity", "Documents",
            "Active document counts for each business entity."),

        // Compliance
        new("compliance-scorecard", "Compliance Scorecard", "Compliance",
            "Live readiness score for every framework, for the current entity."),
        new("compliance-gaps", "Compliance Gaps", "Compliance",
            "Requirements that are missing or expired, by framework."),
        new("compliance-renewals", "Upcoming Renewals", "Compliance",
            "Documents satisfying a requirement that expire within 120 days."),

        // Sharing
        new("shares-active", "Active External Shares", "Sharing",
            "Currently active document shares and their recipients."),
        new("shares-engagement", "Share View Engagement", "Sharing",
            "View counts and last-viewed time per active share."),
        new("shares-by-permission", "Shares by Permission Level", "Sharing",
            "External shares grouped by the granted permission level."),

        // Data Rooms
        new("rooms-summary", "Data Room Summary", "Data Rooms",
            "Every room with its status, guest count, and document count."),
        new("rooms-guest-activity", "Guest Activity by Room", "Data Rooms",
            "View events logged per room, ranked by engagement."),

        // Engagements
        new("engagements-pipeline", "Engagements by Type & Status", "Engagements",
            "Projects, vendors, and clients grouped by status."),
        new("engagements-checklist", "Checklist Completion", "Engagements",
            "Received vs. required documents for each engagement."),

        // Proposals
        new("proposals-pipeline", "Proposal Pipeline by Stage", "Proposals",
            "Opportunity count and value at each Kanban stage."),
        new("proposals-winloss", "Win/Loss Summary", "Proposals",
            "Won, lost, and open proposals with win rate and value."),
    };

    public static bool Exists(string key) => Reports.Any(r => r.Key == key);

    public static ReportDefinition? Find(string key) => Reports.FirstOrDefault(r => r.Key == key);
}

public record ReportDefinition(string Key, string Title, string Category, string Description);
