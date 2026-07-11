namespace Quorid.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a single tenant. Every query is filtered by
/// <see cref="TenantId"/> and every insert must set it — enforced in the
/// persistence layer via a global query filter and a save interceptor.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
