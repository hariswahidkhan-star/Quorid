using Quorid.Application.Common.Models;
using Quorid.Domain.Entities;

namespace Quorid.Application.Common.Interfaces;

/// <summary>Issues JWT access/refresh tokens for authenticated users.</summary>
public interface IJwtTokenService
{
    TokenPair CreateTokens(User user);

    /// <summary>
    /// Validates a refresh token and returns the user id and tenant id it was
    /// issued for, or null if invalid/expired.
    /// </summary>
    (Guid UserId, Guid TenantId)? ValidateRefreshToken(string refreshToken);
}
