using DataDiode.Authorization.Constants;
using DataDiode.Authorization.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataDiode.Authorization.Controllers;

public class RoleBody
{
    public string? Name { get; set; }
    public string? NameAr { get; set; }
    public string? Description { get; set; }
    public List<int> PermissionIds { get; set; } = new();
}

public class UserRolesBody
{
    public List<int> RoleIds { get; set; } = new();
}

/// <summary>The roles screen and the "assign roles to users" screen.</summary>
[ApiController]
[Route("api/admin")]
public class RolesAdminController : ControllerBase
{
    private readonly AppDb _db;
    private readonly IAuditService _audit;
    private readonly IIdentityService _identity;

    public RolesAdminController(AppDb db, IAuditService audit, IIdentityService identity)
    {
        _db = db;
        _audit = audit;
        _identity = identity;
    }

    private Task AuditAsync(string action, string entityId, string details) =>
        _audit.LogAsync(action, _identity.GetCurrentUserId(), _identity.GetCurrentUserName(), "Role", entityId, details,
                        ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(), userAgent: Request.Headers.UserAgent.ToString());

    // ------------------------------------------------------------------ permissions + roles
    [HasPermission(Permissions.RolesManage)]
    [HttpGet("permissions")]
    public async Task<IActionResult> PermissionList()
    {
        var rows = await _db.Permissions.AsNoTracking().OrderBy(p => p.GroupName).ThenBy(p => p.PermissionId).ToListAsync();
        return Ok(rows.Select(p => new { permissionId = p.PermissionId, code = p.Code, nameAr = p.NameAr, nameEn = p.NameEn, group = p.GroupName }));
    }

    [HasPermission(Permissions.RolesManage)]
    [HttpGet("roles")]
    public async Task<IActionResult> Roles()
    {
        var roles = await _db.Roles.AsNoTracking().Include(r => r.RolePermissions).OrderBy(r => r.RoleId).ToListAsync();
        var counts = await _db.UserRoles.AsNoTracking().GroupBy(x => x.RoleId).Select(g => new { g.Key, N = g.Count() }).ToListAsync();
        return Ok(roles.Select(r => new
        {
            roleId = r.RoleId, name = r.Name, nameAr = r.NameAr, description = r.Description, isSystem = r.IsSystem,
            userCount = counts.Where(c => c.Key == r.RoleId).Select(c => c.N).FirstOrDefault(),
            permissionIds = r.RolePermissions.Select(x => x.PermissionId).ToList()
        }));
    }

    /// <summary>The role names only (no permission needed beyond assigning roles): used by the "users and roles" screen.</summary>
    [HasPermission(Permissions.UserRolesManage)]
    [HttpGet("role-names")]
    public async Task<IActionResult> RoleNames()
    {
        var roles = await _db.Roles.AsNoTracking().OrderBy(r => r.RoleId).Select(r => new { roleId = r.RoleId, name = r.Name, nameAr = r.NameAr }).ToListAsync();
        return Ok(roles);
    }

    [HasPermission(Permissions.RolesManage)]
    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole([FromBody] RoleBody b)
    {
        var name = (b.Name ?? "").Trim();
        if (name.Length < 2) throw new BusinessException("The role name is required.");
        if (name.Length > 80) throw new BusinessException("The role name is too long (max 80 characters).");
        if (await _db.Roles.AnyAsync(r => r.Name == name)) throw new BusinessException("A role with this name already exists.", 409);
        var role = new Role { Name = name, NameAr = Clean(b.NameAr), Description = Clean(b.Description), IsSystem = false };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();
        await SetPermissionsAsync(role.RoleId, b.PermissionIds);
        await AuditAsync(AuditActions.RoleCreated, role.RoleId.ToString(), "Role " + role.Name + " created");
        return Ok(new { roleId = role.RoleId });
    }

    [HasPermission(Permissions.RolesManage)]
    [HttpPut("roles/{id:int}")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] RoleBody b)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == id);
        if (role == null) return NotFound();
        if (!role.IsSystem)
        {
            var name = (b.Name ?? "").Trim();
            if (name.Length < 2) throw new BusinessException("The role name is required.");
            if (name.Length > 80) throw new BusinessException("The role name is too long (max 80 characters).");
            if (await _db.Roles.AnyAsync(r => r.Name == name && r.RoleId != id)) throw new BusinessException("A role with this name already exists.", 409);
            role.Name = name;
        }
        role.NameAr = Clean(b.NameAr);
        role.Description = Clean(b.Description);

        // the Admin role must always keep the two permissions that manage roles, or nobody could fix it afterwards
        var ids = new HashSet<int>(b.PermissionIds ?? new List<int>());
        if (role.Name == AuthRoles.Admin)
        {
            var must = await _db.Permissions.AsNoTracking().Where(p => p.Code == Permissions.RolesManage || p.Code == Permissions.UserRolesManage).Select(p => p.PermissionId).ToListAsync();
            foreach (var m in must) ids.Add(m);
        }
        await _db.SaveChangesAsync();
        await SetPermissionsAsync(id, ids.ToList());
        await AuditAsync(AuditActions.RoleUpdated, id.ToString(), "Role " + role.Name + " updated, " + ids.Count + " permissions");
        return Ok(new { roleId = id });
    }

    [HasPermission(Permissions.RolesManage)]
    [HttpDelete("roles/{id:int}")]
    public async Task<IActionResult> DeleteRole(int id)
    {
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == id);
        if (role == null) return NotFound();
        if (role.IsSystem) throw new BusinessException("A system role cannot be deleted.", 409);
        if (await _db.UserRoles.AnyAsync(x => x.RoleId == id))
            throw new BusinessException("This role is assigned to users. Remove it from them first.", 409);
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync();
        await AuditAsync(AuditActions.RoleDeleted, id.ToString(), "Role " + role.Name + " deleted");
        return Ok(new { ok = true });
    }

    // ------------------------------------------------------------------ users and their roles
    [HasPermission(Permissions.UserRolesManage)]
    [HttpGet("user-roles")]
    public async Task<IActionResult> UserRoles(string? search = null, int take = 300)
    {
        var q = _db.Users.AsNoTracking().Include(u => u.UserRoles).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(u => u.Username.Contains(s) || (u.FullName != null && u.FullName.Contains(s)) || (u.Email != null && u.Email.Contains(s)));
        }
        var users = await q.OrderBy(u => u.Username).Take(Math.Clamp(take, 1, 1000)).ToListAsync();
        return Ok(users.Select(u => new
        {
            userId = u.Id, username = u.Username, fullName = u.DisplayName, email = u.Email, externalId = u.ExternalId, isActive = u.IsActive,
            roleIds = u.UserRoles.Select(x => x.RoleId).ToList()
        }));
    }

    [HasPermission(Permissions.UserRolesManage)]
    [HttpPut("user-roles/{userId:int}")]
    public async Task<IActionResult> SetUserRoles(int userId, [FromBody] UserRolesBody b)
    {
        var user = await _db.Users.Include(u => u.UserRoles).FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return NotFound();
        var wanted = new HashSet<int>(b.RoleIds ?? new List<int>());
        var valid = await _db.Roles.AsNoTracking().Where(r => wanted.Contains(r.RoleId)).Select(r => r.RoleId).ToListAsync();
        if (valid.Count != wanted.Count) throw new BusinessException("Unknown role.");

        await using var tx = await _db.Database.BeginTransactionAsync();
        foreach (var ur in user.UserRoles.Where(x => !wanted.Contains(x.RoleId)).ToList()) _db.UserRoles.Remove(ur);
        foreach (var rid in wanted.Where(r => !user.UserRoles.Any(x => x.RoleId == r))) _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = rid });
        // the legacy IsAdmin flag follows the Admin role, for the code that still reads it
        var admin = await _db.Roles.AsNoTracking().Where(r => r.Name == AuthRoles.Admin).Select(r => (int?)r.RoleId).FirstOrDefaultAsync();
        user.IsAdmin = admin.HasValue && wanted.Contains(admin.Value);
        await _db.SaveChangesAsync();

        // never leave the system without anyone who can assign roles
        var holders = await _db.UserRoles.AsNoTracking()
            .Where(x => x.User.IsActive && x.Role.RolePermissions.Any(rp => rp.Permission.Code == Permissions.UserRolesManage))
            .Select(x => x.UserId).Distinct().CountAsync();
        if (holders == 0)
        {
            await tx.RollbackAsync();
            throw new BusinessException("This change cannot be made: at least one user must stay able to assign roles.", 409);
        }
        await tx.CommitAsync();
        await AuditAsync(AuditActions.RolesAssigned, userId.ToString(), "User " + user.Username + " now has roles [" + string.Join(",", wanted) + "]");
        return Ok(new { ok = true });
    }

    private async Task SetPermissionsAsync(int roleId, List<int>? permissionIds)
    {
        var wanted = new HashSet<int>(permissionIds ?? new List<int>());
        var valid = await _db.Permissions.AsNoTracking().Where(p => wanted.Contains(p.PermissionId)).Select(p => p.PermissionId).ToListAsync();
        var current = await _db.RolePermissions.Where(x => x.RoleId == roleId).ToListAsync();
        foreach (var c in current.Where(c => !valid.Contains(c.PermissionId))) _db.RolePermissions.Remove(c);
        foreach (var pid in valid.Where(v => !current.Any(c => c.PermissionId == v))) _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = pid });
        await _db.SaveChangesAsync();
    }

    private static string? Clean(string? s)
    {
        s = (s ?? "").Trim();
        return s.Length == 0 ? null : s;
    }
}
