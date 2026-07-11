using Quorid.Application.Common.Interfaces;
using Quorid.Infrastructure.Identity;

namespace Quorid.Api.Middleware;

/// <summary>
/// Reads the tenant_id claim from the authenticated principal and pins it into
/// the request-scoped <see cref="ITenantContext"/>, which drives the DbContext's
/// global tenant query filter.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, ITenantContext tenantContext)
    {
        var tenantClaim = context.User.FindFirst(JwtTokenService.TenantClaim)?.Value;
        if (Guid.TryParse(tenantClaim, out var tenantId))
        {
            tenantContext.SetTenant(tenantId);
        }

        await _next(context);
    }
}
