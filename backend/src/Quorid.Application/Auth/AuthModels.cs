namespace Quorid.Application.Auth;

/// <summary>Registers a new tenant with its first entity and owner user.</summary>
public record RegisterRequest(
    string TenantName,
    string EntityName,
    string FullName,
    string Email,
    string Password);

/// <summary>
/// Authenticates a user. Email is unique per tenant, so an optional tenant
/// domain disambiguates when the same email exists in multiple tenants.
/// </summary>
public record LoginRequest(string Email, string Password, string? TenantDomain = null);

public record RefreshRequest(string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserSummary User);

public record UserSummary(Guid Id, string Email, string FullName, Guid TenantId, Guid EntityId);
