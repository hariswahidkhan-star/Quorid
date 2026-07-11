using Quorid.Domain.Common;

namespace Quorid.Domain.Entities;

/// <summary>
/// A category of data room (Investor DD, Audit, Tax, Banking, Legal, Compliance,
/// Custom). System room types ship with default templates.
/// </summary>
public class RoomType : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>e.g. DR-INV, DR-AUD, DR-TAX.</summary>
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystem { get; set; }

    public string Status { get; set; } = "active";
}
