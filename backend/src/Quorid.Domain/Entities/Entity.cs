using Quorid.Domain.Common;
using Quorid.Domain.Enums;

namespace Quorid.Domain.Entities;

/// <summary>
/// A business entity (company, subsidiary, client). One tenant may own many
/// entities; documents and rooms each belong to exactly one entity.
/// </summary>
public class Entity : BaseEntity, ITenantScoped, IAuditableEntity
{
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Short code, unique per tenant (e.g. "ACG-001").</summary>
    public string Code { get; set; } = string.Empty;

    public EntityType Type { get; set; } = EntityType.Llc;

    public Guid? ParentEntityId { get; set; }

    public string? Ein { get; set; }
    public string? State { get; set; }
    public DateOnly? FormationDate { get; set; }

    /// <summary>6-digit NAICS industry code.</summary>
    public string? Naics { get; set; }

    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    public Guid? PrimaryContactId { get; set; }
    public decimal? Revenue { get; set; }
    public int? EmployeeCount { get; set; }
    public string? LogoUrl { get; set; }

    public EntityStatus Status { get; set; } = EntityStatus.Active;

    public DateTime UpdatedAt { get; set; }
}
