namespace Quorid.Domain.Common;

/// <summary>
/// Entities that track their last modification. <see cref="BaseEntity.CreatedAt"/>
/// covers creation; this adds an update timestamp maintained by the save interceptor.
/// </summary>
public interface IAuditableEntity
{
    DateTime UpdatedAt { get; set; }
}
