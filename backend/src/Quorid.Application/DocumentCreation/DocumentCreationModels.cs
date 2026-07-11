namespace Quorid.Application.DocumentCreation;

// ---- Templates ----

public record TemplateDto(string Key, string Name, string Category, string? Description, bool IsSystem);

public record CreateTemplateRequest(string Name, string Category, string? Description, string Body);

// ---- Drafts ----

public record GenerateDraftRequest(
    string? TemplateKey,
    string DocumentType,
    string? Title,
    string Prompt,
    string? Recipient,
    Dictionary<string, string>? Variables);

public record UpdateDraftRequest(string? Title, string? Body, string? Status);

public record ImproveRequest(string? Instruction);

public record DraftListItemDto(
    Guid Id, string Title, string? TemplateKey, string Status, bool Finalized, DateTime UpdatedAt);

public record DraftDetailDto(
    Guid Id, string Title, string? TemplateKey, string? Prompt, string Body,
    string Status, Guid? DocumentId, DateTime UpdatedAt);

// ---- Generation contract ----

/// <summary>Everything the generator needs to compose a draft body, offline or via a model.</summary>
public record GenerationRequest(
    string DocumentType,
    string? TemplateBody,
    string Prompt,
    IReadOnlyDictionary<string, string> Tokens);
