namespace Quorid.Domain.Common;

/// <summary>
/// Quorid never hard-deletes documents, users, or rooms. Entities implementing
/// this are archived/deactivated via a status column instead of being removed.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }
}
