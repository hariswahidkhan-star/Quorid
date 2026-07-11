namespace Quorid.Application.DocumentCreation;

/// <summary>
/// Seeded system templates for the creation studio (Module 4). Bodies use
/// <c>{{token}}</c> placeholders resolved from the entity profile and user input;
/// <c>{{body}}</c> is where the generator expands the free-text prompt.
/// </summary>
public static class TemplateCatalog
{
    public static readonly IReadOnlyList<TemplateSeed> Templates = new List<TemplateSeed>
    {
        new("nda", "Mutual Non-Disclosure Agreement", "Legal",
            "A reciprocal confidentiality agreement between two parties.",
            "MUTUAL NON-DISCLOSURE AGREEMENT\n\n" +
            "This Agreement is entered into as of {{date}} by and between {{entity.name}} " +
            "(\"Disclosing Party\") and {{recipient}} (\"Receiving Party\").\n\n" +
            "{{body}}\n\n" +
            "Each party agrees to protect the other's Confidential Information with the same " +
            "degree of care it uses for its own, and to use such information solely for the " +
            "purpose of the parties' business relationship.\n\n" +
            "Signed,\n{{entity.name}}"),

        new("engagement-letter", "Engagement Letter", "Business Development",
            "Confirms the scope, terms, and fees of a professional engagement.",
            "{{date}}\n\n{{recipient}}\n\nDear {{recipient}},\n\n" +
            "Thank you for the opportunity to work with you. This letter confirms the terms of " +
            "our engagement.\n\n{{body}}\n\n" +
            "We look forward to a productive relationship.\n\nSincerely,\n{{entity.name}}"),

        new("statement-of-work", "Statement of Work", "Business Development",
            "Defines deliverables, timeline, and acceptance criteria for a project.",
            "STATEMENT OF WORK\n\nPrepared by: {{entity.name}}\nPrepared for: {{recipient}}\nDate: {{date}}\n\n" +
            "1. OVERVIEW\n{{body}}\n\n" +
            "2. DELIVERABLES\nAs described above and mutually agreed.\n\n" +
            "3. ACCEPTANCE\nDeliverables are accepted upon written confirmation by {{recipient}}."),

        new("company-overview", "Company Overview", "Marketing",
            "A one-page introduction to the business for prospects and partners.",
            "{{entity.name}} — Company Overview\n\n" +
            "{{body}}\n\n" +
            "Entity type: {{entity.type}}\nState: {{entity.state}}\nWebsite: {{entity.website}}\n\n" +
            "For more information, contact us at {{entity.phone}}."),

        new("capability-statement", "Capability Statement", "Business Development",
            "A concise summary of core competencies for bids and RFPs.",
            "CAPABILITY STATEMENT\n{{entity.name}}\n\n" +
            "CORE COMPETENCIES\n{{body}}\n\n" +
            "COMPANY DATA\nEntity type: {{entity.type}}\nState of formation: {{entity.state}}\n" +
            "EIN: {{entity.ein}}\n\nCONTACT\n{{entity.phone}} · {{entity.website}}"),

        new("cover-letter", "Proposal Cover Letter", "Business Development",
            "A persuasive cover letter to accompany a proposal submission.",
            "{{date}}\n\nDear {{recipient}},\n\n" +
            "On behalf of {{entity.name}}, I am pleased to submit the enclosed proposal.\n\n" +
            "{{body}}\n\n" +
            "We would welcome the opportunity to discuss how we can meet your needs.\n\n" +
            "Sincerely,\n{{entity.name}}"),

        new("board-resolution", "Board Resolution", "Legal",
            "Records a formal decision adopted by the board of directors.",
            "RESOLUTION OF THE BOARD OF DIRECTORS OF {{entity.name}}\n\n" +
            "Adopted: {{date}}\n\n" +
            "WHEREAS, the Board has considered the matter below; now, therefore, it is\n\n" +
            "RESOLVED, that:\n{{body}}\n\n" +
            "The foregoing resolution was duly adopted and remains in full force and effect."),

        new("vendor-letter", "Vendor Onboarding Letter", "Operations",
            "Welcomes a new vendor and requests onboarding documentation.",
            "{{date}}\n\nDear {{recipient}},\n\n" +
            "Welcome to the {{entity.name}} vendor network. To complete your onboarding, we ask " +
            "that you provide the documentation described below.\n\n{{body}}\n\n" +
            "Thank you for partnering with us.\n\nRegards,\n{{entity.name}}"),

        new("compliance-attestation", "Compliance Attestation", "Compliance",
            "A signed statement attesting to compliance with a standard or policy.",
            "COMPLIANCE ATTESTATION\n\n" +
            "{{entity.name}} hereby attests, as of {{date}}, to the following:\n\n{{body}}\n\n" +
            "This attestation is made to the best of our knowledge and belief.\n\n" +
            "Authorized signature,\n{{entity.name}}"),

        new("reference-request", "Reference Request", "Business Development",
            "Requests a reference or testimonial from a past client.",
            "{{date}}\n\nDear {{recipient}},\n\n" +
            "It was a pleasure working with you. We are writing to request a brief reference " +
            "regarding your experience with {{entity.name}}.\n\n{{body}}\n\n" +
            "Thank you for your time and support.\n\nWarm regards,\n{{entity.name}}"),
    };

    public static TemplateSeed? Find(string key) => Templates.FirstOrDefault(t => t.Key == key);
}

public record TemplateSeed(string Key, string Name, string Category, string Description, string Body);
