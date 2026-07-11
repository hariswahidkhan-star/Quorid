using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Quorid.Application.Auth;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Common.Security;
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>Registration, login, and token refresh (spec §7.1 Auth).</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync).AllowAnonymous();
        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous();

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        ApplicationDbContext db,
        IPasswordHasher hasher,
        IJwtTokenService jwt)
    {
        if (string.IsNullOrWhiteSpace(request.TenantName) ||
            string.IsNullOrWhiteSpace(request.EntityName) ||
            string.IsNullOrWhiteSpace(request.FullName))
        {
            return Results.BadRequest(new { error = "Tenant name, entity name, and full name are required." });
        }

        if (!IsValidEmail(request.Email))
            return Results.BadRequest(new { error = "A valid email address is required." });

        if (!IsValidPassword(request.Password))
        {
            return Results.BadRequest(new
            {
                error = "Password must be at least 8 characters and include uppercase, lowercase, number, and special characters."
            });
        }

        var tenant = new Tenant
        {
            Name = request.TenantName.Trim(),
            Plan = PlanTier.Starter,
            Status = TenantStatus.Active
        };
        db.Tenants.Add(tenant);

        var ownerRole = SystemRole(tenant.Id, SystemRoles.Owner, "Full-access owner", SystemRoles.FullPermissions());
        var adminRole = SystemRole(tenant.Id, SystemRoles.Admin, "Administrator", SystemRoles.FullPermissions());
        var viewerRole = SystemRole(tenant.Id, SystemRoles.Viewer, "Read-only viewer", SystemRoles.ViewerPermissions());
        db.Roles.AddRange(ownerRole, adminRole, viewerRole);

        var entity = new Entity
        {
            TenantId = tenant.Id,
            Name = request.EntityName.Trim(),
            Code = GenerateEntityCode(request.EntityName),
            Type = EntityType.Llc,
            Status = EntityStatus.Active
        };
        db.Entities.Add(entity);

        var user = new User
        {
            TenantId = tenant.Id,
            EntityId = entity.Id,
            Email = request.Email.Trim().ToLowerInvariant(),
            FullName = request.FullName.Trim(),
            RoleId = ownerRole.Id,
            Status = UserStatus.Active,
            PasswordHash = hasher.Hash(request.Password)
        };
        db.Users.Add(user);

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenant.Id,
            UserId = user.Id,
            Action = "TENANT_REGISTERED",
            ResourceType = nameof(Tenant),
            ResourceId = tenant.Id
        });

        await db.SaveChangesAsync();

        return Ok(jwt, user);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        ApplicationDbContext db,
        IPasswordHasher hasher,
        IJwtTokenService jwt)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();

        // Login happens before a tenant is known, so bypass the tenant filter.
        var candidates = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Email == email)
            .ToListAsync();

        var user = candidates.FirstOrDefault();
        if (candidates.Count > 1 && !string.IsNullOrWhiteSpace(request.TenantDomain))
        {
            var tenantIds = await db.Tenants.IgnoreQueryFilters()
                .Where(t => t.Domain == request.TenantDomain)
                .Select(t => t.Id)
                .ToListAsync();

            user = candidates.FirstOrDefault(u => tenantIds.Contains(u.TenantId)) ?? user;
        }

        if (user is null || string.IsNullOrEmpty(user.PasswordHash) ||
            !hasher.Verify(request.Password, user.PasswordHash))
        {
            return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);
        }

        if (user.Status != UserStatus.Active)
            return Results.Json(new { error = "Account is not active." }, statusCode: 403);

        if (user.LockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow)
            return Results.Json(new { error = "Account is temporarily locked." }, statusCode: 403);

        user.LastLoginAt = DateTime.UtcNow;
        user.FailedLoginCount = 0;
        await db.SaveChangesAsync();

        return Ok(jwt, user);
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        ApplicationDbContext db,
        IJwtTokenService jwt)
    {
        var validated = jwt.ValidateRefreshToken(request.RefreshToken);
        if (validated is null)
            return Results.Json(new { error = "Invalid or expired refresh token." }, statusCode: 401);

        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == validated.Value.UserId);

        if (user is null || user.Status != UserStatus.Active)
            return Results.Json(new { error = "User not found or inactive." }, statusCode: 401);

        return Ok(jwt, user);
    }

    private static IResult Ok(IJwtTokenService jwt, User user)
    {
        var tokens = jwt.CreateTokens(user);
        return Results.Ok(new AuthResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.AccessTokenExpiresAt,
            new UserSummary(user.Id, user.Email, user.FullName, user.TenantId, user.EntityId)));
    }

    private static Role SystemRole(Guid tenantId, string name, string description, IReadOnlyDictionary<string, bool> permissions) =>
        new()
        {
            TenantId = tenantId,
            Name = name,
            Description = description,
            IsSystem = true,
            Permissions = JsonSerializer.Serialize(permissions)
        };

    private static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && Regex.IsMatch(email, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$");

    private static bool IsValidPassword(string? password) =>
        !string.IsNullOrEmpty(password) &&
        password.Length >= 8 &&
        password.Any(char.IsUpper) &&
        password.Any(char.IsLower) &&
        password.Any(char.IsDigit) &&
        password.Any(c => !char.IsLetterOrDigit(c));

    private static string GenerateEntityCode(string name)
    {
        var letters = new string(name.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        if (letters.Length < 3) letters = (letters + "XXX")[..3];
        return $"{letters}-{DateTime.UtcNow.Ticks % 1000:D3}";
    }
}
