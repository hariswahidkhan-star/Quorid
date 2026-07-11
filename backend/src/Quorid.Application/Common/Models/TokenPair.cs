namespace Quorid.Application.Common.Models;

/// <summary>
/// An access token (15 min) plus a refresh token (7 days), per spec §10.
/// </summary>
public record TokenPair(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt);
