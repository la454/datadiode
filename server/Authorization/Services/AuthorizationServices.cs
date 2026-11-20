using System.Security.Claims;
using DataDiode.Authorization.Constants;
using DataDiode.Authorization.Dtos;
using DataDiode.Authorization.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataDiode.Authorization.Services;

/// <summary>Permissions come from the user's roles (UserRoles -> RolePermissions -> Permissions); read once per request.</summary>
public class PermissionService : IPermissionService
{
    private readonly AppDb _db;
    private readonly Dictionary<int, IReadOnlySet<string>> _codes = new();
    private readonly Dictionary<int, IReadOnlyList<string>> _roles = new();

    public PermissionService(AppDb db)
    {
        _db = db;
    }

    public async Task<IReadOnlySet<string>> CodesOfAsync(int userId)
    {
        if (_codes.TryGetValue(userId, out var cached)) return cached;
        var roleIds = await RoleIdsOfAsync(userId);
        var list = await _db.RolePermissions.AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Code)
            .Distinct().ToListAsync();
        IReadOnlySet<string> set = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        _codes[userId] = set;
        return set;
    }

    public async Task<bool> HasAsync(int userId, string code)
    {
        return (await CodesOfAsync(userId)).Contains(code);
    }

    /// <summary>
    /// The user's roles from UserRoles. A user managed only inside DataDiode (no dashboard GUID) that has no UserRoles rows yet
    /// falls back to the old Users.IsAdmin flag (Admin or User). A dashboard user with no rows has NO role, so no access, until an admin assigns one.
    /// </summary>
    private async Task<List<int>> RoleIdsOfAsync(int userId)
    {
        // a disabled or deleted user has no roles at all, so a token issued before that stops working at once
        var u = await _db.Users.AsNoTracking().Where(x => x.Id == userId).Select(x => new { x.IsActive, x.IsAdmin, x.ExternalId }).FirstOrDefaultAsync();
        if (u == null || !u.IsActive) return new List<int>();
        var ids = await _db.UserRoles.AsNoTracking().Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync();
        if (ids.Count > 0) return ids;
        if (!u.ExternalId.HasValue)
        {
            var name = u.IsAdmin ? AuthRoles.Admin : AuthRoles.User;
            var id = await _db.Roles.AsNoTracking().Where(r => r.Name == name).Select(r => (int?)r.RoleId).FirstOrDefaultAsync();
            if (id.HasValue) ids.Add(id.Value);
        }
        return ids;
    }

    public async Task<string> BehavesAsAsync(int userId)
    {
        var codes = await CodesOfAsync(userId);
        if (codes.Any(c => !Permissions.FileCodes.Contains(c, StringComparer.OrdinalIgnoreCase))) return AuthRoles.Admin;
        return codes.Contains(Permissions.FilesView) ? AuthRoles.User : "None";
    }

    public async Task<IReadOnlyList<string>> RoleNamesOfAsync(int userId)
    {
        if (_roles.TryGetValue(userId, out var cached)) return cached;
        var roleIds = await RoleIdsOfAsync(userId);
        var names = await _db.Roles.AsNoTracking().Where(r => roleIds.Contains(r.RoleId)).Select(r => r.Name).Distinct().ToListAsync();
        IReadOnlyList<string> list = names;
        _roles[userId] = list;
        return list;
    }
}

public class IdentityService : IIdentityService
{
    private readonly IHttpContextAccessor _http;

    public IdentityService(IHttpContextAccessor http)
    {
        _http = http;
    }

    public int GetCurrentUserId()
    {
        var v = _http.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(v, out var id) ? id : -1;
    }

    public string GetCurrentUserName()
    {
        return _http.HttpContext?.User?.FindFirst(ClaimTypes.Name)?.Value ?? "";
    }
}

public class AuditService : IAuditService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AuditService> _log;

    public AuditService(IServiceScopeFactory scopes, ILogger<AuditService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    public async Task LogAsync(string action, int userId, string? username = null, string? entityType = null, string? entityId = null,
                               string? details = null, bool success = true, string? errorMessage = null, string? ipAddress = null, string? userAgent = null)
    {
        try
        {
            static string? Cut(string? v, int n) => v != null && v.Length > n ? v.Substring(0, n) : v;
            // its own scope and context: a request whose context holds a failed change must still be able to write its audit row
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            db.AuditLogs.Add(new AuditLog
            {
                Action = Cut(action, 100)!,
                UserId = userId > 0 ? userId : null,
                Username = Cut(username, 200),
                EntityType = Cut(entityType, 100),
                EntityId = Cut(entityId, 200),
                Details = Cut(details, 2000),
                Success = success,
                ErrorMessage = Cut(errorMessage, 500),
                IpAddress = Cut(ipAddress, 45),
                UserAgent = Cut(userAgent, 300),
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "The audit log could not be written.");
        }
    }
}

public class RoleAuthorizationService : IRoleAuthorizationService
{
    public bool IsInRole(ClaimsPrincipal user, string role)
    {
        return user != null && user.Identity != null && user.Identity.IsAuthenticated && user.IsInRole(role);
    }

    public bool IsAdmin(ClaimsPrincipal user) => IsInRole(user, AuthRoles.Admin);

    public bool IsUser(ClaimsPrincipal user) => IsInRole(user, AuthRoles.User);

    public int? GetUserId(ClaimsPrincipal user)
    {
        var claim = user?.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null && int.TryParse(claim.Value, out var id) ? id : null;
    }

    public AccessInfoDto GetAccessInfo(ClaimsPrincipal user)
    {
        var role = user?.FindFirst(ClaimTypes.Role)?.Value;
        return new AccessInfoDto
        {
            UserId = GetUserId(user!) ?? 0,
            Role = role,
            IsAdmin = IsAdmin(user!),
            IsUser = IsUser(user!)
        };
    }
}
