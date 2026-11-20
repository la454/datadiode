using System.Security.Claims;
using DataDiode.Authentication.Interfaces;
using DataDiode.Authentication.Models;
using DataDiode.Authorization.Interfaces;
using DataDiode.Models.Token;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace DataDiode.Authentication;

/// <summary>
/// Turns whatever the token says into DataDiode's own identity:
///  - NameIdentifier = the integer DataDiode user id (the dashboard token carries a GUID),
///  - Name           = the user's display name,
///  - Role claims    = the user's roles from UserRoles (a user can have many),
///  - "sid"          = a session id.
/// </summary>
public class DataDiodeClaimsTransformation : IClaimsTransformation
{
    private const string Done = "datadiode:done";

    private readonly IUserResolver _resolver;
    private readonly IPermissionService _permissions;
    private readonly IHttpContextAccessor _http;

    public DataDiodeClaimsTransformation(IUserResolver resolver, IPermissionService permissions, IHttpContextAccessor http)
    {
        _resolver = resolver;
        _permissions = permissions;
        _http = http;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity == null || !principal.Identity.IsAuthenticated || principal.HasClaim(c => c.Type == Done)) return principal;

        var own = principal.FindFirst("sid") != null;     // a token issued by DataDiode's own login
        int userId;
        string? displayName = null;
        if (own)
        {
            if (!int.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId)) return principal;
        }
        else
        {
            // a dashboard token: find (or create) the DataDiode user
            var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value ?? principal.FindFirst("nameid")?.Value;
            Guid? guid = Guid.TryParse(sub, out var g) ? g : null;
            var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value;
            var name = principal.FindFirst(ClaimTypes.Name)?.Value ?? principal.FindFirst("name")?.Value;
            if (_http.HttpContext?.Items["datadiode:cookieUser"] is DashboardUser cookie)
            {
                guid ??= cookie.Id;
                email ??= cookie.Email;
                name ??= cookie.FullName;
            }
            if (guid.HasValue && (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name))
                && _http.HttpContext?.RequestServices.GetService<ExternalAuthDbContext>() is { } ext)
            {
                try
                {
                    var d = await ext.Users.AsNoTracking().FirstOrDefaultAsync(x => x.PublicUserId == guid.Value);
                    if (d != null)
                    {
                        if (string.IsNullOrWhiteSpace(email)) email = d.Email;
                        if (string.IsNullOrWhiteSpace(name)) name = !string.IsNullOrWhiteSpace(d.DisplayName) ? d.DisplayName : d.UserName;
                    }
                }
                catch { /* the dashboard user table is optional: fall back to the claims */ }
            }
            var user = await _resolver.ResolveAsync(guid, email, name);
            if (user == null || !user.IsActive) return principal;     // known to the dashboard but unknown / disabled here: no DataDiode identity
            userId = user.Id;
            displayName = user.DisplayName;
        }

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(Done, "1"));
        foreach (var c in principal.FindAll(ClaimTypes.NameIdentifier).ToList()) (c.Subject as ClaimsIdentity)?.RemoveClaim(c);
        foreach (var c in principal.FindAll(ClaimTypes.Role).ToList()) (c.Subject as ClaimsIdentity)?.RemoveClaim(c);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        if (!own)
        {
            identity.AddClaim(new Claim("sid", Guid.NewGuid().ToString("N")));
            if (!string.IsNullOrWhiteSpace(displayName) && principal.FindFirst(ClaimTypes.Name) == null)
                identity.AddClaim(new Claim(ClaimTypes.Name, displayName));
        }
        foreach (var role in await _permissions.RoleNamesOfAsync(userId))
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        principal.AddIdentity(identity);
        return principal;
    }
}
