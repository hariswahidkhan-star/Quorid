namespace Quorid.Application.Common.Security;

/// <summary>
/// The 15 role permissions from spec §Screen 9 (Role Management). Stored as
/// boolean flags in <c>roles.permissions</c> JSON.
/// </summary>
public static class Permissions
{
    public const string ViewDocuments = "viewDocuments";
    public const string Download = "download";
    public const string Upload = "upload";
    public const string EditMetadata = "editMetadata";
    public const string DeleteDocuments = "deleteDocuments";
    public const string CreateRooms = "createRooms";
    public const string ManageUsers = "manageUsers";
    public const string ViewAuditLog = "viewAuditLog";
    public const string ManagePermissions = "managePermissions";
    public const string ConfigureSettings = "configureSettings";
    public const string ShareExternally = "shareExternally";
    public const string ApproveDocuments = "approveDocuments";
    public const string ManageCompliance = "manageCompliance";
    public const string ExportData = "exportData";
    public const string ManageBilling = "manageBilling";

    public static readonly string[] All =
    {
        ViewDocuments, Download, Upload, EditMetadata, DeleteDocuments,
        CreateRooms, ManageUsers, ViewAuditLog, ManagePermissions,
        ConfigureSettings, ShareExternally, ApproveDocuments,
        ManageCompliance, ExportData, ManageBilling
    };
}
