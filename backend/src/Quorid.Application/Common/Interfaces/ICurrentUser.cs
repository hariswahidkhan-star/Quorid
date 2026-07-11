namespace Quorid.Application.Common.Interfaces;

/// <summary>Identity of the authenticated caller for the current request.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }

    Guid? TenantId { get; }

    Guid? EntityId { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }
}
