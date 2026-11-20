using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using DataDiode.Authentication.Interfaces;
using DataDiode.Authentication.Models;
using DataDiode.Authorization.Constants;
using DataDiode.Authorization.Interfaces;
using DataDiode.Models.Token;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DataDiode.Authentication.Services;

/// <summary>DataDiode's own login token (the "Bearer" scheme): signed with Jwt:Key, with Jwt:Issuer / Jwt:Audience.</summary>
public class TokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string Create(AppUser user, string roleName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config[AppSettings.JwtKey]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.Role, roleName),
            new Claim("sid", Guid.NewGuid().ToString("N"))
        };
        var hours = double.Parse(_config["Jwt:ExpiresHours"] ?? "8", System.Globalization.CultureInfo.InvariantCulture);
        var token = new JwtSecurityToken(
            issuer: _config[AppSettings.JwtIssuer],
            audience: _config[AppSettings.JwtAudience],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(hours),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class TokenValidatorService : ITokenValidatorService
{
    private readonly AccessGateSessionsContext _tokenDb;

    public TokenValidatorService(AccessGateSessionsContext tokenDb)
    {
        _tokenDb = tokenDb;
    }

    public async Task<AccessTokenSession?> GetAccessTokenSessionAsync(string userId, string jti)
    {
        if (!Guid.TryParse(userId, out var uid) || string.IsNullOrWhiteSpace(jti)) return null;
        return await _tokenDb.AccessTokenSessions.AsNoTracking().FirstOrDefaultAsync(t => t.Jti == jti && t.UserId == uid);
    }

    public async Task<AccessTokenSession?> GetSessionByRefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;
        return await _tokenDb.AccessTokenSessions.AsNoTracking()
            .Where(t => t.RefreshToken == refreshToken)
            .OrderByDescending(t => t.IssuedAt)
            .FirstOrDefaultAsync();
    }
}

public class UserResolver : IUserResolver
{
    private readonly AppDb _db;
    private readonly IConfiguration _config;
    private readonly FileStore _store;
    private readonly IPasswordHasher<AppUser> _hasher;
    private readonly IAuditService _audit;

    public UserResolver(AppDb db, IConfiguration config, FileStore store, IPasswordHasher<AppUser> hasher, IAuditService audit)
    {
        _db = db;
        _config = config;
        _store = store;
        _hasher = hasher;
        _audit = audit;
    }

    public async Task<AppUser?> ResolveAsync(Guid? externalId, string? email, string? fullName)
    {
        email = (email ?? "").Trim().ToLowerInvariant();
        AppUser? user = null;
        if (externalId.HasValue) user = await _db.Users.FirstOrDefaultAsync(u => u.ExternalId == externalId.Value);
        if (user == null && email.Length > 0)
        {
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
            // an existing DataDiode user is linked to the dashboard account the first time it signs in. If that user is already linked to
            // a DIFFERENT dashboard account, it is another person who happens to share the e-mail: do not hand them this user.
            if (user != null && externalId.HasValue)
            {
                if (!user.ExternalId.HasValue)
                {
                    user.ExternalId = externalId;
                    await _db.SaveChangesAsync();
                }
                else if (user.ExternalId != externalId) user = null;
            }
        }
        if (user != null) return user;

        if (!_config.GetValue(AppSettings.AuthAutoProvision, true) || !externalId.HasValue) return null;

        // first time this dashboard user reaches DataDiode: a record with no role (and so no permissions) until an admin assigns roles.
        var name = string.IsNullOrWhiteSpace(fullName) ? (email.Length > 0 ? email : externalId.Value.ToString("N")) : fullName.Trim();
        var login = email.Length > 0 ? email : "dashboard-" + externalId.Value.ToString("N");
        if (await _db.Users.AnyAsync(u => u.Username == login)) login = login + "-" + externalId.Value.ToString("N")[..6];
        user = new AppUser
        {
            ExternalId = externalId,
            Username = login,
            FullName = name,
            Email = email.Length > 0 && !await _db.Users.AnyAsync(u => u.Email == email) ? email : null,
            IsActive = true,
            IsAdmin = false,
            FolderName = Guid.NewGuid().ToString("N"),
            QuotaBytes = 100L * 1024 * 1024,
            CreatedAt = DateTime.UtcNow
        };
        // dashboard users never sign in with a DataDiode password: the stored hash is of a random secret nobody knows
        user.PasswordHash = _hasher.HashPassword(user, Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)));
        try { Directory.CreateDirectory(_store.UserDir(user)); } catch { /* the folder is created on demand later */ }
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await _audit.LogAsync(AuditActions.DashboardUserProvisioned, user.Id, user.DisplayName, "User", user.Id.ToString(),
                              "First sign-in from the dashboard (no role yet).");
        return user;
    }
}

/// <summary>
/// Sign-in with the dashboard's refresh cookie. That cookie holds an opaque refresh token (not a JWT), so there is no signature to check:
/// the token is looked up in the dashboard's AccessTokenSessions table (column RefreshToken). The session must not be revoked and its
/// access token must not have expired (the dashboard refreshes it, which also renews the cookie). The row's UserId (a GUID) is the user.
/// </summary>
public class DashboardSessionHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _config;
    private readonly ITokenValidatorService _sessions;

    public DashboardSessionHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
                                   IConfiguration config, ITokenValidatorService sessions) : base(options, logger, encoder)
    {
        _config = config;
        _sessions = sessions;
    }

    /// <summary>The value of the first configured cookie that is present and is NOT a JWT / JSON (so it must be an opaque refresh token).</summary>
    public static string? OpaqueCookieValue(HttpRequest request, IConfiguration config)
    {
        foreach (var name in new[] { config[AppSettings.AuthRefreshCookieName], config[AppSettings.AuthCookieName] ?? "learny_token" })
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (request.Cookies.TryGetValue(name, out var raw) && !string.IsNullOrWhiteSpace(raw) && !DashboardTokenCookie.TryRead(raw, out _))
                return raw.Trim().Trim('"');
        }
        return null;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var refresh = OpaqueCookieValue(Request, _config);
        if (refresh == null) return AuthenticateResult.NoResult();

        try
        {
            var session = await _sessions.GetSessionByRefreshTokenAsync(refresh);
            if (session == null) return AuthenticateResult.Fail("No dashboard session holds this refresh token.");
            if (session.RevokedAt != null) return AuthenticateResult.Fail("The session was revoked.");
            if (session.ExpiresAt <= DateTime.UtcNow)
            {
                Logger.LogWarning("The dashboard session expired at {At} (UTC now {Now}); the dashboard must refresh it.", session.ExpiresAt, DateTime.UtcNow);
                return AuthenticateResult.Fail("The session has expired.");
            }

            var identity = new ClaimsIdentity(Scheme.Name);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()));
            identity.AddClaim(new Claim("sub", session.UserId.ToString()));
            identity.AddClaim(new Claim("jti", session.Jti ?? ""));
            Context.Items["datadiode:fromCookie"] = true;      // a cookie sign-in: the CSRF guard applies
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "The dashboard session check failed (is ConnectionStrings:ExternalToken right?); the request is refused.");
            return AuthenticateResult.Fail("The session check failed.");
        }
    }
}
