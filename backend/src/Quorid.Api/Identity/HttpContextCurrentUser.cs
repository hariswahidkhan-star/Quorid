using System.Security.Claims;
using Quorid.Application.Common.Interfaces;
using Quorid.Infrastructure.Identity;

namespace Quorid.Api.Identity;

/// <summary>Exposes the authenticated caller's identity from the HTTP context.</summary>
public class HttpContextCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpContextCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    public Guid? UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public Guid? TenantId =>
        Guid.TryParse(User?.FindFirstValue(JwtTokenService.TenantClaim), out var id) ? id : null;

    public Guid? EntityId =>
        Guid.TryParse(User?.FindFirstValue(JwtTokenService.EntityClaim), out var id) ? id : null;

    public string? Email => User?.FindFirstValue("email");

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
