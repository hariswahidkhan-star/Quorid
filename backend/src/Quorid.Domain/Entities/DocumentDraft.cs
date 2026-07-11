using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// A generated, editable document draft (Module 4). Produced by the generator from
/// a prompt (optionally seeded by a <see cref="DocumentTemplate"/>), edited in the
/// studio, and — on finalize — written to blob storage and promoted into the vault
/// as a real <see cref="Document"/> (see <see cref="DocumentId"/>).
/// </summary>
public class DocumentDraft : BaseEntity, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }

    public Guid EntityId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>The template this draft was generated from, if any.</summary>
    public string? TemplateKey { get; set; }

    /// <summary>The instruction the generator worked from, kept for regeneration.</summary>
    public string? Prompt { get; set; }

    /// <summary>Current draft content (edited freely in the studio).</summary>
    public string Body { get; set; } = string.Empty;

    public DraftStatus Status { get; set; } = DraftStatus.Draft;

    /// <summary>Set once finalized — the vault document this draft became.</summary>
    public Guid? DocumentId { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }
}
