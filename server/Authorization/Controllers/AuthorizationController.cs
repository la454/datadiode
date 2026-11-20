using DataDiode.Authorization.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DataDiode.Authorization.Controllers;

[ApiController]
[Route("api/authorization")]
[Authorize]
public class AuthorizationController : ControllerBase
{
    private readonly IRoleAuthorizationService _authz;
    private readonly IPermissionService _permissions;

    public AuthorizationController(IRoleAuthorizationService authz, IPermissionService permissions)
    {
        _authz = authz;
        _permissions = permissions;
    }

    /// <summary>The signed-in user's id, role flags, role names and permission codes (the menu and the route guards use them).</summary>
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var info = _authz.GetAccessInfo(User);
        if (info.UserId > 0)
        {
            var behaves = await _permissions.BehavesAsAsync(info.UserId);
            info.Role = behaves;
            info.IsAdmin = behaves == "Admin";
            info.IsUser = behaves == "User";
            info.Roles = (await _permissions.RoleNamesOfAsync(info.UserId)).ToList();
            info.Permissions = (await _permissions.CodesOfAsync(info.UserId)).OrderBy(c => c).ToList();
        }
        return Ok(info);
    }

    /// <summary>Whether the signed-in user has the given role.</summary>
    [HttpGet("has-role/{role}")]
    public IActionResult HasRole(string role)
    {
        return Ok(new Dtos.RoleCheckResultDto { Role = role, Allowed = _authz.IsInRole(User, role) });
    }
}
