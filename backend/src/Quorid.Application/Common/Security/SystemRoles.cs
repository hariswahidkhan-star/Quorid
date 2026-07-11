namespace Quorid.Application.Common.Security;

/// <summary>Names of the built-in, non-deletable system roles.</summary>
public static class SystemRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Editor = "Editor";
    public const string Viewer = "Viewer";

    /// <summary>Owner and Admin hold every permission.</summary>
    public static IReadOnlyDictionary<string, bool> FullPermissions() =>
        Permissions.All.ToDictionary(p => p, _ => true);

    /// <summary>Viewer can only view documents and the audit log is off by default.</summary>
    public static IReadOnlyDictionary<string, bool> ViewerPermissions() =>
        Permissions.All.ToDictionary(p => p, p => p == Permissions.ViewDocuments);
}
