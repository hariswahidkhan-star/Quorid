namespace Quorid.Infrastructure.Identity;

/// <summary>JWT settings bound from the "Jwt" configuration section.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "quorid";

    public string Audience { get; set; } = "quorid";

    /// <summary>HMAC signing key. Must be at least 32 bytes. Set via configuration/secret.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;
}
