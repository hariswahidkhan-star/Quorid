using Quorid.Application.Common.Security;

namespace Quorid.Api.Authorization;

/// <summary>
/// Authorization policy names, one per permission in <see cref="Permissions.All"/>.
/// A policy is named <c>perm:&lt;permissionKey&gt;</c>; the constants below expose the
/// handful enforced today so endpoints can reference them without magic strings.
/// </summary>
public static class Policies
{
    public const string Prefix = "perm:";

    public static string For(string permission) => Prefix + permission;

    public static readonly string ManageUsers = For(Permissions.ManageUsers);
    public static readonly string ManagePermissions = For(Permissions.ManagePermissions);
    public static readonly string ConfigureSettings = For(Permissions.ConfigureSettings);
    public static readonly string ManageBilling = For(Permissions.ManageBilling);
}
