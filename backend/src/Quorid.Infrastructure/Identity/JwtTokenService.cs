using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Common.Models;
using Quorid.Domain.Entities;

namespace Quorid.Infrastructure.Identity;

/// <summary>
/// Issues signed access and refresh JWTs. Access tokens carry tenant_id and
/// entity_id claims used by the tenant-resolution middleware.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    public const string TokenTypeClaim = "token_type";
    public const string TenantClaim = "tenant_id";
    public const string EntityClaim = "entity_id";

    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public TokenPair CreateTokens(User user)
    {
        var now = DateTime.UtcNow;
        var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);
        var refreshExpires = now.AddDays(_options.RefreshTokenDays);

        var access = BuildToken(user, now, accessExpires, "access");
        var refresh = BuildToken(user, now, refreshExpires, "refresh");

        return new TokenPair(access, refresh, accessExpires);
    }

    public (Guid UserId, Guid TenantId)? ValidateRefreshToken(string refreshToken)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        try
        {
            var principal = handler.ValidateToken(refreshToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            if (principal.FindFirst(TokenTypeClaim)?.Value != "refresh")
                return null;

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var tenantId = principal.FindFirst(TenantClaim)?.Value;

            if (Guid.TryParse(userId, out var uid) && Guid.TryParse(tenantId, out var tid))
                return (uid, tid);

            return null;
        }
        catch
        {
            return null;
        }
    }

    private string BuildToken(User user, DateTime notBefore, DateTime expires, string tokenType)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("email", user.Email),
            new(TenantClaim, user.TenantId.ToString()),
            new(EntityClaim, user.EntityId.ToString()),
            new(TokenTypeClaim, tokenType),
            new("jti", Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
