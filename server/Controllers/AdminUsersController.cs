using DataDiode.Authorization;
using DataDiode.Authorization.Constants;
using DataDiode.Authorization.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataDiode.Controllers;

/// <summary>Users (folder, file types, quota, group) and groups. Needs the "users.manage" permission.</summary>
[ApiController]
[Route("api/admin")]
[Authorize]
[HasPermission(Permissions.UsersManage)]
public class AdminUsersController(AppDb db, FileStore store, IPasswordHasher<AppUser> hasher, IAuditService audit, IIdentityService identity,
                                  IPermissionService permissions) : AppControllerBase
{
    // ------------------------------------------------------------------ helpers
    private static string NormalizeExts(string? s) => string.Join(",", FileStore.ParseExts(s));

    /// <summary>The normalized absolute folder path, or null (+ error) if invalid. Blank = auto-managed folder.</summary>
    private static string? ResolveFolder(string? input, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(input)) return "";
        var t = input.Trim().Trim('"').Trim(); // tolerate a pasted path wrapped in quotes; inner spaces are kept
        if (!Path.IsPathFullyQualified(t))
        {
            error = "Folder must be an absolute path (e.g. D:\\Incoming\\alice or \\\\server\\share\\My Folder).";
            return null;
        }
        string full;
        try { full = Path.GetFullPath(t); }
        catch { error = "Invalid folder path."; return null; }
        // The folder is created/shared by the domain administrator: we only check that it exists.
        if (!Directory.Exists(full))
        {
            error = $"Folder '{full}' does not exist or the server account ({Environment.UserDomainName}\\{Environment.UserName}) cannot access it.";
            return null;
        }
        return full;
    }

    private static string? CleanEmail(string? s) { s = (s ?? "").Trim().ToLowerInvariant(); return s.Length == 0 ? null : s; }
    private static string? Clean(string? s) { s = (s ?? "").Trim(); return s.Length == 0 ? null : s; }
    private static bool LooksLikeEmail(string s) => s.Length <= 256 && s.Contains('@') && !s.StartsWith('@') && !s.EndsWith('@') && !s.Contains(' ');

    /// <summary>Role names of each user from UserRoles; a local user with no row falls back to the legacy IsAdmin flag.</summary>
    private async Task<Dictionary<int, List<string>>> RolesOfAsync(IReadOnlyCollection<AppUser> users)
    {
        var ids = users.Select(u => u.Id).ToList();
        var rows = await db.UserRoles.AsNoTracking().Where(x => ids.Contains(x.UserId)).Select(x => new { x.UserId, x.Role.Name }).ToListAsync();
        var map = rows.GroupBy(x => x.UserId).ToDictionary(g => g.Key, g => g.Select(x => x.Name).OrderBy(n => n).ToList());
        foreach (var u in users)
            if (!map.ContainsKey(u.Id))
                map[u.Id] = u.ExternalId.HasValue ? new List<string>() : new List<string> { u.IsAdmin ? AuthRoles.Admin : AuthRoles.User };
        return map;
    }

    private object UserDto(AppUser u, IReadOnlyList<string> roles) => new
    {
        u.Id, u.Username, u.Email, fullName = u.FullName, u.IsActive,
        fromDashboard = u.ExternalId.HasValue,
        isAdmin = roles.Contains(AuthRoles.Admin),
        roles,
        u.GroupId,
        folderPath = store.UserDir(u),
        allowedExtensions = FileStore.ParseExts(u.AllowedExtensions),
        quotaMb = u.QuotaBytes / (1024 * 1024),
        usedBytes = store.UsedBytes(u),
    };

    private async Task<object> UserDtoAsync(AppUser u) => UserDto(u, (await RolesOfAsync(new[] { u }))[u.Id]);

    /// <summary>How many active users (other than the given one) can still assign roles.</summary>
    private async Task<int> OtherRoleManagersAsync(int exceptUserId) =>
        await db.UserRoles.AsNoTracking()
            .Where(x => x.UserId != exceptUserId && x.User.IsActive && x.Role.RolePermissions.Any(rp => rp.Permission.Code == Permissions.UserRolesManage))
            .Select(x => x.UserId).Distinct().CountAsync();

    // ------------------------------------------------------------------ users
    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
        var roles = await RolesOfAsync(users);
        return Ok(users.Select(u => UserDto(u, roles[u.Id])));
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest r)
    {
        var username = (r.Username ?? "").Trim();
        if (username.Length == 0 || r.Password is not { Length: >= 6 })
            return BadRequest(new { message = "Username required and password must be at least 6 characters." });
        if (await db.Users.AnyAsync(u => u.Username == username))
            return Conflict(new { message = "Username already exists." });
        var email = CleanEmail(r.Email);
        if (email != null && !LooksLikeEmail(email)) return BadRequest(new { message = "The e-mail address is not valid." });
        if (email != null && await db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "A user with this e-mail already exists." });
        var folder = ResolveFolder(r.FolderPath, out var ferr);
        if (folder is null) return BadRequest(new { message = ferr });
        if (r.GroupId is { } gid && !await db.Groups.AnyAsync(g => g.Id == gid))
            return BadRequest(new { message = "Group not found." });
        var roleName = r.IsAdmin ? AuthRoles.Admin : AuthRoles.User;
        var role = await db.Roles.FirstOrDefaultAsync(x => x.Name == roleName);
        if (role is null) return Problem("The system role '" + roleName + "' is missing (run database/Add_External_Auth.sql).");

        var u = new AppUser
        {
            Username = username,
            Email = email,
            FullName = Clean(r.FullName),
            IsAdmin = r.IsAdmin,
            IsActive = true,
            FolderName = Guid.NewGuid().ToString("N"),
            AllowedExtensions = NormalizeExts(r.AllowedExtensions),
            QuotaBytes = (r.QuotaMb ?? 100) * 1024 * 1024,
            FolderPath = folder,
            GroupId = r.GroupId,
            CreatedAt = DateTime.UtcNow,
        };
        u.PasswordHash = hasher.HashPassword(u, r.Password);
        // Only the built-in default folder (blank path) is created by us; admin-supplied folders must already exist.
        if (string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(store.UserDir(u));
        u.UserRoles.Add(new UserRole { Role = role });
        db.Users.Add(u);
        await db.SaveChangesAsync();
        await AuditAsync(audit, identity, AuditActions.UserCreated, "User", u.Id.ToString(), "User " + u.Username + " created with role " + roleName + ".");
        return Created($"/api/admin/users/{u.Id}", await UserDtoAsync(u));
    }

    [HttpPut("users/{id:int}/settings")]
    public async Task<IActionResult> SaveSettings(int id, [FromBody] UpdateSettingsRequest r)
    {
        if (await db.Users.FindAsync(id) is not { } u) return NotFound();
        if (r.QuotaMb < 0) return BadRequest(new { message = "Quota must be >= 0." });
        var folder = ResolveFolder(r.FolderPath, out var ferr);
        if (folder is null) return BadRequest(new { message = ferr });
        if (r.GroupId is { } gid && !await db.Groups.AnyAsync(g => g.Id == gid))
            return BadRequest(new { message = "Group not found." });
        if (r.IsActive == false && u.Id == Uid) return BadRequest(new { message = "You cannot disable yourself." });
        if (r.IsActive == false && u.IsActive && await OtherRoleManagersAsync(u.Id) == 0 && (await permissions.CodesOfAsync(u.Id)).Contains(Permissions.UserRolesManage))
            return BadRequest(new { message = "At least one active user must stay able to assign roles." });

        if (r.Email != null)
        {
            var email = CleanEmail(r.Email);
            if (email != null && !LooksLikeEmail(email)) return BadRequest(new { message = "The e-mail address is not valid." });
            if (email != null && await db.Users.AnyAsync(x => x.Email == email && x.Id != id))
                return Conflict(new { message = "A user with this e-mail already exists." });
            u.Email = email;
        }
        if (r.FullName != null) u.FullName = Clean(r.FullName);
        if (r.IsActive.HasValue) u.IsActive = r.IsActive.Value;
        u.AllowedExtensions = NormalizeExts(r.AllowedExtensions);
        u.GroupId = r.GroupId;
        if (folder.Length > 0) u.FolderPath = folder;
        else if (string.IsNullOrWhiteSpace(u.FolderPath)) Directory.CreateDirectory(store.UserDir(u));
        u.QuotaBytes = r.QuotaMb * 1024 * 1024;
        await db.SaveChangesAsync();
        await AuditAsync(audit, identity, AuditActions.UserUpdated, "User", id.ToString(), "Settings of " + u.Username + " changed (quota " + r.QuotaMb + " MB" + (r.IsActive == false ? ", disabled" : "") + ").");
        return Ok(await UserDtoAsync(u));
    }

    [HttpPut("users/{id:int}/password")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest r)
    {
        if (await db.Users.FindAsync(id) is not { } u) return NotFound();
        if (r.Password is not { Length: >= 6 }) return BadRequest(new { message = "Password must be at least 6 characters." });
        u.PasswordHash = hasher.HashPassword(u, r.Password);
        await db.SaveChangesAsync();
        await AuditAsync(audit, identity, AuditActions.PasswordReset, "User", id.ToString(), "Password of " + u.Username + " reset.");
        return NoContent();
    }

    [HttpDelete("users/{id:int}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (await db.Users.FindAsync(id) is not { } u) return NotFound();
        if (u.Id == Uid) return BadRequest(new { message = "You cannot delete yourself." });
        if (await OtherRoleManagersAsync(u.Id) == 0 && (await permissions.CodesOfAsync(u.Id)).Contains(Permissions.UserRolesManage))
            return BadRequest(new { message = "At least one active user must stay able to assign roles." });
        // Only auto-managed folders are removed; admin-assigned folders are left untouched.
        if (string.IsNullOrWhiteSpace(u.FolderPath))
        {
            var dir = store.UserDir(u);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        db.Users.Remove(u);
        await db.SaveChangesAsync();
        await AuditAsync(audit, identity, AuditActions.UserDeleted, "User", id.ToString(), "User " + u.Username + " deleted.");
        return NoContent();
    }

    // ------------------------------------------------------------------ groups
    [HttpGet("groups")]
    public async Task<IActionResult> Groups() =>
        Ok(await db.Groups.OrderBy(g => g.Name).Select(g => new { g.Id, g.Name, memberCount = g.Users.Count }).ToListAsync());

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] GroupRequest r)
    {
        var name = (r.Name ?? "").Trim();
        if (name.Length is 0 or > 100) return BadRequest(new { message = "Group name is required (max 100 characters)." });
        if (await db.Groups.AnyAsync(g => g.Name == name)) return Conflict(new { message = "Group already exists." });
        var g = new Group { Name = name };
        db.Groups.Add(g);
        await db.SaveChangesAsync();
        await AuditAsync(audit, identity, AuditActions.GroupCreated, "Group", g.Id.ToString(), "Group " + name + " created.");
        return Created($"/api/admin/groups/{g.Id}", new { g.Id, g.Name, memberCount = 0 });
    }

    [HttpPut("groups/{id:int}")]
    public async Task<IActionResult> RenameGroup(int id, [FromBody] GroupRequest r)
    {
        if (await db.Groups.FindAsync(id) is not { } g) return NotFound();
        var name = (r.Name ?? "").Trim();
        if (name.Length is 0 or > 100) return BadRequest(new { message = "Group name is required (max 100 characters)." });
        if (await db.Groups.AnyAsync(x => x.Name == name && x.Id != id)) return Conflict(new { message = "Group already exists." });
        var old = g.Name;
        g.Name = name;
        await db.SaveChangesAsync();
        await AuditAsync(audit, identity, AuditActions.GroupRenamed, "Group", id.ToString(), "Group " + old + " renamed to " + name + ".");
        return Ok(new { g.Id, g.Name, memberCount = await db.Users.CountAsync(u => u.GroupId == id) });
    }

    [HttpDelete("groups/{id:int}")]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        if (await db.Groups.FindAsync(id) is not { } g) return NotFound();
        await db.Users.Where(u => u.GroupId == id).ExecuteUpdateAsync(s => s.SetProperty(u => u.GroupId, (int?)null)); // members become ungrouped
        db.Groups.Remove(g);
        await db.SaveChangesAsync();
        await AuditAsync(audit, identity, AuditActions.GroupDeleted, "Group", id.ToString(), "Group " + g.Name + " deleted.");
        return NoContent();
    }
}
