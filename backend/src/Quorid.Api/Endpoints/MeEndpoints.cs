using Microsoft.EntityFrameworkCore;
using Quorid.Application.Common.Interfaces;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>Current-user profile (spec §7.5 GET /api/users/me).</summary>
public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users").RequireAuthorization();

        group.MapGet("/me", async (ICurrentUser currentUser, ApplicationDbContext db) =>
        {
            if (currentUser.UserId is not { } userId)
                return Results.Unauthorized();

            var user = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.FullName,
                    u.TenantId,
                    u.EntityId,
                    Role = u.Role != null ? u.Role.Name : null,
                    u.Status,
                    u.MfaEnabled,
                    u.LastLoginAt
                })
                .FirstOrDefaultAsync();

            return user is null ? Results.NotFound() : Results.Ok(user);
        });

        return app;
    }
}
