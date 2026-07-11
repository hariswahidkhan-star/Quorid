using Microsoft.AspNetCore.Authorization;

namespace Quorid.Api.Authorization;

/// <summary>
/// Requires the authenticated caller's role to grant a specific permission from the
/// 15-permission catalog (spec §Screen 9). Evaluated by <see cref="PermissionAuthorizationHandler"/>.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permission) => Permission = permission;

    public string Permission { get; }
}
