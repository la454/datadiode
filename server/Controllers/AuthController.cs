using DataDiode.Authentication.Services;
using DataDiode.Authorization.Constants;
using DataDiode.Authorization.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataDiode.Controllers;

/// <summary>Sign-in: DataDiode's own login (username + password -> JWT), the dashboard (cookie) mode and "who am I".</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(AppDb db, FileStore store, IPasswordHasher<AppUser> hasher, TokenService tokens, IPermissionService permissions,
                            IAuditService audit, IConfiguration config) : AppControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest r)
    {
        // dashboard sign-in is on: the dashboard is the only way in, DataDiode's own password login is closed
        if (config.GetValue<bool>(AppSettings.AuthEnabled))
            throw new BusinessException("Sign-in is done through the dashboard.", 403);

        var name = (r.Username ?? "").Trim();
        var email = name.ToLowerInvariant();
        var u = name.Length == 0 ? null : await db.Users.FirstOrDefaultAsync(x => x.Username == name || (x.Email != null && x.Email == email));
        if (u is null || !u.IsActive || hasher.VerifyHashedPassword(u, u.PasswordHash, r.Password ?? "") == PasswordVerificationResult.Failed)
        {
            await audit.LogAsync(AuditActions.LoginFailed, u?.Id ?? -1, name, "User", u?.Id.ToString(), "Wrong username or password, or a disabled user.",
                                 false, "Invalid credentials", ClientIp, Request.Headers.UserAgent.ToString());
            throw new BusinessException("Invalid username or password.", 401);
        }

        var role = await permissions.BehavesAsAsync(u.Id);
        await audit.LogAsync(AuditActions.Login, u.Id, u.DisplayName, "User", u.Id.ToString(), "Signed in with username and password.",
                             true, null, ClientIp, Request.Headers.UserAgent.ToString());
        return Ok(new { token = tokens.Create(u, role), user = await SelfDtoAsync(u) });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me() =>
        await CurrentUserAsync(db) is { } u ? Ok(await SelfDtoAsync(u)) : Unauthorized();

    /// <summary>How the app signs people in: through the dashboard (the cookie) or DataDiode's own login page.</summary>
    [AllowAnonymous]
    [HttpGet("config")]
    public IActionResult Config() =>
        Ok(new { dashboardMode = config.GetValue<bool>(AppSettings.AuthEnabled), dashboardUrl = config[AppSettings.AuthDashboardUrl] ?? "" });

    // What a signed-in user may see about themself: no id, no server folder path, no group.
    private async Task<object> SelfDtoAsync(AppUser u) => new
    {
        u.Username,
        fullName = u.DisplayName,
        u.Email,
        role = await permissions.BehavesAsAsync(u.Id),           // "Admin" / "User" / "None" (never a custom role name, so the app cannot loop)
        isAdmin = await permissions.BehavesAsAsync(u.Id) == AuthRoles.Admin,
        fromDashboard = u.ExternalId.HasValue,
        allowedExtensions = FileStore.ParseExts(u.AllowedExtensions),
        quotaMb = u.QuotaBytes / (1024 * 1024),
        usedBytes = store.UsedBytes(u),
    };
}
