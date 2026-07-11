namespace Quorid.Domain.Common;

/// <summary>
/// Root for all persisted entities. UUID (CHAR(36)) primary key and a
/// creation timestamp, both required across every Quorid table.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; }
}
