using DataDiode.Authorization.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataDiode.Authorization.Controllers;

/// <summary>The audit log screen: sign-ins, refused calls, user / role / file changes. Needs "audit.view".</summary>
[ApiController]
[Route("api/admin/audit-logs")]
public class AuditLogsController : ControllerBase
{
    private readonly AppDb _db;

    public AuditLogsController(AppDb db)
    {
        _db = db;
    }

    [HasPermission(Permissions.AuditView)]
    [HttpGet]
    public async Task<IActionResult> List(string? action = null, string? search = null, bool? success = null, int take = 200)
    {
        var q = _db.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(action)) { var a = action.Trim(); q = q.Where(x => x.Action == a); }
        if (success.HasValue) q = q.Where(x => x.Success == success.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            q = q.Where(x => (x.Username != null && x.Username.Contains(s)) || (x.Details != null && x.Details.Contains(s))
                          || (x.EntityId != null && x.EntityId.Contains(s)) || (x.IpAddress != null && x.IpAddress.Contains(s)));
        }
        var rows = await q.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.AuditLogId).Take(Math.Clamp(take, 1, 1000)).ToListAsync();
        return Ok(rows.Select(x => new
        {
            id = x.AuditLogId, userId = x.UserId, username = x.Username, action = x.Action, entityType = x.EntityType, entityId = x.EntityId,
            details = x.Details, ipAddress = x.IpAddress, success = x.Success, errorMessage = x.ErrorMessage,
            createdAt = DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)
        }));
    }

    [HasPermission(Permissions.AuditView)]
    [HttpGet("actions")]
    public async Task<IActionResult> Actions() =>
        Ok(await _db.AuditLogs.AsNoTracking().Select(x => x.Action).Distinct().OrderBy(x => x).ToListAsync());
}
