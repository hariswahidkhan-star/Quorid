using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>An ordered reference to a vault document within a proposal package.</summary>
public class ProposalDocument : BaseEntity
{
    public Guid ProposalId { get; set; }

    public Guid DocumentId { get; set; }

    public int DisplayOrder { get; set; }

    public Guid AddedBy { get; set; }

    public Proposal? Proposal { get; set; }
}
