namespace DataDiode.Authorization.Constants;

/// <summary>Configuration keys (appsettings.json) of the sign-in and authorization code.</summary>
public static class AppSettings
{
    // DataDiode's own login token
    public const string JwtKey = "Jwt:Key";
    public const string JwtIssuer = "Jwt:Issuer";
    public const string JwtAudience = "Jwt:Audience";

    // Dashboard single sign-on
    public const string AuthEnabled = "Auth:Enabled";
    public const string AuthCookieName = "Auth:CookieName";
    public const string AuthRefreshCookieName = "Auth:RefreshCookieName";
    public const string AuthAutoProvision = "Auth:AutoProvision";
    public const string AuthDashboardUrl = "Auth:DashboardUrl";
    public const string AuthJwtKey = "Auth:Jwt:Key";
    public const string AuthJwtIssuer = "Auth:Jwt:Issuer";
    public const string AuthJwtAudience = "Auth:Jwt:Audience";

    // Connection strings
    public const string ConnectionDefault = "Default";
    public const string ConnectionExternalToken = "ExternalToken";
    public const string ConnectionExternalAuth = "ExternalAuth";
}

public static class AuthRoles
{
    public const string Admin = "Admin";
    public const string User = "User";
}

public static class AuthPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string UserOnly = "UserOnly";
}

/// <summary>Permission codes (the same codes as the Permissions table). Use them like [HasPermission(Permissions.UsersManage)].</summary>
public static class Permissions
{
    // Files (what an ordinary user does in the assigned folder)
    public const string FilesView = "files.view";
    public const string FilesDownload = "files.download";
    public const string FilesDelete = "files.delete";

    // People
    public const string UsersManage = "users.manage";

    // Security
    public const string RolesManage = "roles.manage";
    public const string UserRolesManage = "userroles.manage";
    public const string AuditView = "audit.view";

    /// <summary>A user that has only these codes behaves as an ordinary "User"; any other code makes them an "Admin".</summary>
    public static readonly string[] FileCodes = { FilesView, FilesDownload, FilesDelete };

    public static IEnumerable<string> GetAll()
    {
        return typeof(Permissions)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => f.GetValue(null)?.ToString())
            .Where(v => v != null)!;
    }
}

public static class PolicyNames
{
    public static string FromPermission(string permission) => HasPermissionAttribute.Prefix + permission;

    public static readonly string UsersManage = FromPermission(Permissions.UsersManage);
    public static readonly string RolesManage = FromPermission(Permissions.RolesManage);
    public static readonly string UserRolesManage = FromPermission(Permissions.UserRolesManage);
    public static readonly string AuditView = FromPermission(Permissions.AuditView);
}

public static class AuditActions
{
    // Access
    public const string UnauthorizedAccess = "UnauthorizedAccess";
    public const string ForbiddenAccess = "ForbiddenAccess";

    // Sign-in
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string DashboardUserProvisioned = "DashboardUserProvisioned";

    // Users and groups
    public const string UserCreated = "UserCreated";
    public const string UserUpdated = "UserUpdated";
    public const string UserDeleted = "UserDeleted";
    public const string PasswordReset = "PasswordReset";
    public const string GroupCreated = "GroupCreated";
    public const string GroupRenamed = "GroupRenamed";
    public const string GroupDeleted = "GroupDeleted";

    // Roles
    public const string RoleCreated = "RoleCreated";
    public const string RoleUpdated = "RoleUpdated";
    public const string RoleDeleted = "RoleDeleted";
    public const string RolesAssigned = "RolesAssigned";

    // Files
    public const string FileDownloaded = "FileDownloaded";
    public const string FileDeleted = "FileDeleted";
}
