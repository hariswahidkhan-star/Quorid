using Quorid.Application.Common.Interfaces;
using Quorid.Application.Vault;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Module 2 — Identity Vault (spec §Module 2 / §7.6). Aggregated per-entity
/// profile with verification tiers and health score, plus the cross-validation
/// engine. When no entityId is supplied, the caller's own entity is used.
/// </summary>
public static class VaultEndpoints
{
    public static IEndpointRouteBuilder MapVaultEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vault").WithTags("Vault").RequireAuthorization();

        group.MapGet("/", async (IVaultService vault, ICurrentUser user, Guid? entityId, CancellationToken ct) =>
        {
            if ((entityId ?? user.EntityId) is not { } id)
                return Results.BadRequest(new { error = "No entity specified." });

            var profile = await vault.GetProfileAsync(id, ct);
            return profile is null ? Results.NotFound() : Results.Ok(profile);
        });

        group.MapPost("/cross-validate", CrossValidateAsync);

        // Spec §7.6 alias.
        app.MapPost("/api/ai/cross-validate", CrossValidateAsync)
            .RequireAuthorization()
            .WithTags("AI");

        return app;
    }

    private static async Task<IResult> CrossValidateAsync(
        IVaultService vault, ICurrentUser user, Guid? entityId, CancellationToken ct)
    {
        if ((entityId ?? user.EntityId) is not { } id)
            return Results.BadRequest(new { error = "No entity specified." });

        var report = await vault.CrossValidateAsync(id, ct);
        return report is null ? Results.NotFound() : Results.Ok(report);
    }
}
