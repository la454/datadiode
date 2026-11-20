using System.Security.Claims;
using DataDiode.Authorization.Dtos;

namespace DataDiode.Authorization.Interfaces;

public interface IAuditService
{
    /// <summary>Writes one row to AuditLogs. Never throws: a failing audit must not break the request.</summary>
    Task LogAsync(string action, int userId, string? username = null, string? entityType = null, string? entityId = null,
                  string? details = null, bool success = true, string? errorMessage = null, string? ipAddress = null, string? userAgent = null);
}

/// <summary>The signed-in user of the current request, as a DataDiode user (dashboard users are already mapped to a DataDiode id).</summary>
public interface IIdentityService
{
    /// <summary>The DataDiode user id of the current request, or -1 when nobody known to DataDiode is signed in.</summary>
    int GetCurrentUserId();

    string GetCurrentUserName();
}

public interface IPermissionService
{
    /// <summary>Every permission code the user has through any of their roles.</summary>
    Task<IReadOnlySet<string>> CodesOfAsync(int userId);

    Task<bool> HasAsync(int userId, string code);

    /// <summary>"Admin" when the user has any management permission, "User" when they can see files, otherwise "None" (no role yet).</summary>
    Task<string> BehavesAsAsync(int userId);

    /// <summary>Names of the user's roles.</summary>
    Task<IReadOnlyList<string>> RoleNamesOfAsync(int userId);
}

public interface IRoleAuthorizationService
{
    bool IsInRole(ClaimsPrincipal user, string role);
    bool IsAdmin(ClaimsPrincipal user);
    bool IsUser(ClaimsPrincipal user);

    /// <summary>Id of the signed-in user, or null when not authenticated.</summary>
    int? GetUserId(ClaimsPrincipal user);

    AccessInfoDto GetAccessInfo(ClaimsPrincipal user);
}
