using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Quorid.Application.Common.Interfaces;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Authorization;

/// <summary>
/// Grants a <see cref="PermissionRequirement"/> when the caller's role carries the
/// permission. Role permissions are the JSON flag map on <c>roles.permissions</c>;
/// Owner and Admin hold every permission, so existing accounts are unaffected.
/// Runs after tenant resolution, so the user lookup is tenant-scoped.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUser _user;

    public PermissionAuthorizationHandler(ApplicationDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (_user.UserId is not { } userId)
            return; // unauthenticated — leave unmet (401/403 upstream)

        var permissionsJson = await _db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.Role != null ? u.Role.Permissions : null)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(permissionsJson))
            return;

        try
        {
            var permissions = JsonSerializer.Deserialize<Dictionary<string, bool>>(permissionsJson);
            if (permissions is not null &&
                permissions.TryGetValue(requirement.Permission, out var granted) && granted)
            {
                context.Succeed(requirement);
            }
        }
        catch (JsonException)
        {
            // Malformed permission map — treat as no grant.
        }
    }
}
