using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>Freeform or AI-suggested tag on a document.</summary>
public class DocumentTag : BaseEntity
{
    public Guid DocumentId { get; set; }

    public string TagName { get; set; } = string.Empty;

    public TagSource Source { get; set; } = TagSource.Manual;
}
