using System.Security.Claims;
using DataDiode.Authorization.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataDiode.Controllers;

/// <summary>Helpers shared by the controllers: who is calling, from where, and the audit line.</summary>
public abstract class AppControllerBase : ControllerBase
{
    /// <summary>The signed-in DataDiode user id (after the claims transformation it is an int for own and dashboard sign-ins alike); -1 when unknown.</summary>
    protected int Uid => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var i) ? i : -1;

    protected string ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";

    protected async Task<AppUser?> CurrentUserAsync(AppDb db)
    {
        var id = Uid;
        if (id <= 0) return null;
        var u = await db.Users.FindAsync(id);
        return u is { IsActive: true } ? u : null;
    }

    protected Task AuditAsync(IAuditService audit, IIdentityService identity, string action, string? entityType, string? entityId, string? details,
                              bool success = true, string? error = null)
        => audit.LogAsync(action, identity.GetCurrentUserId(), identity.GetCurrentUserName(), entityType, entityId, details, success, error,
                          ClientIp, Request.Headers.UserAgent.ToString());
}
