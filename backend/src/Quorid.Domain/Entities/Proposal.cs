using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// A branded proposal / opportunity (spec §Module 10). Moves through Kanban
/// stages; carries an ordered set of vault documents and an optional cover letter.
/// </summary>
public class Proposal : BaseEntity, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }

    public Guid EntityId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? RecipientName { get; set; }

    public OpportunityStage Stage { get; set; } = OpportunityStage.Lead;

    public decimal? Value { get; set; }

    public string? CoverLetter { get; set; }

    public DateOnly? DueDate { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<ProposalDocument> Documents { get; set; } = new List<ProposalDocument>();
}
