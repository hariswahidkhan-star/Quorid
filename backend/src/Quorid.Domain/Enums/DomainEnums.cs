namespace Quorid.Domain.Enums;

/// <summary>Subscription plan for a tenant (see spec §9 Pricing Tiers).</summary>
public enum PlanTier
{
    Starter,
    Business,
    Professional,
    Enterprise
}

public enum TenantStatus
{
    Active,
    Suspended,
    Cancelled
}

/// <summary>Legal structure of a business entity (spec §8.2).</summary>
public enum EntityType
{
    Llc,
    CCorp,
    SCorp,
    Partnership,
    SoleProprietorship,
    Trust,
    NonProfit,
    Government,
    JointVenture
}

public enum EntityStatus
{
    Active,
    Inactive,
    Archived
}

public enum UserStatus
{
    Invited,
    Active,
    Suspended,
    Deactivated
}

public enum GroupStatus
{
    Active,
    Archived
}

/// <summary>Document lifecycle status.</summary>
public enum DocumentStatus
{
    Draft,
    UnderReview,
    Approved,
    Active,
    Expired,
    Archived,
    Superseded
}

/// <summary>4-tier trust model (spec §Module 2).</summary>
public enum VerificationTier
{
    T1, // Self-Reported
    T2, // Cross-Referenced
    T3, // Source-Verified
    T4  // Counter-Party
}

public enum PrivacyLevel
{
    Open,
    Controlled,
    Restricted,
    Locked
}

public enum ClassificationMethod
{
    Ai,
    Manual,
    Override
}

public enum TagSource
{
    Ai,
    Manual
}

public enum UploadJobStatus
{
    Pending,
    Processing,
    Complete,
    PartialError
}

/// <summary>Data room lifecycle (Draft → Active → Extended → Closed → Archived).</summary>
public enum RoomStatus
{
    Draft,
    Active,
    Extended,
    Closed,
    Archived
}

public enum DownloadPolicy
{
    None,
    ViewOnly,
    EncryptedPdf,
    Pdf,
    Original
}

public enum AccessHours
{
    TwentyFourSeven,
    BusinessHours,
    Custom
}

public enum RoomGuestRole
{
    Reviewer,
    Viewer,
    Contributor
}

/// <summary>8-level permission model (spec §Module 5 / Rooms).</summary>
public enum PermissionLevel
{
    None,
    FenceView,
    View,
    EncryptedPdf,
    Print,
    Pdf,
    Original,
    Upload
}

public enum GuestStatus
{
    Invited,
    Active,
    Expired,
    Revoked
}

public enum DocumentViewAction
{
    View,
    Download,
    Print,
    ScreenshotAttempt
}

public enum QaCategory
{
    Financial,
    Legal,
    Technical,
    Operational,
    Tax,
    General
}

public enum QaStatus
{
    Pending,
    Assigned,
    DraftAnswer,
    Published,
    Closed
}

public enum SharingStatus
{
    Active,
    Revoked,
    Expired
}

/// <summary>Discriminates the three engagement registries (Modules 7–9).</summary>
public enum EngagementType
{
    Project,
    Vendor,
    Client
}

public enum EngagementStatus
{
    Active,
    Completed,
    Archived
}

/// <summary>Kanban stages for the opportunity tracker (spec §Module 10).</summary>
public enum OpportunityStage
{
    Lead,
    Qualified,
    Proposal,
    Negotiation,
    Won,
    Lost
}

/// <summary>Lifecycle of an AI-generated document draft (spec §Module 4).</summary>
public enum DraftStatus
{
    Draft,
    InReview,
    Finalized
}
