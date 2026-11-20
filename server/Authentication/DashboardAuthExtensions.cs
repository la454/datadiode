using System.Security.Claims;
using System.Text;
using DataDiode.Authentication.Interfaces;
using DataDiode.Authentication.Models;
using DataDiode.Authentication.Services;
using DataDiode.Authorization.Auth;
using DataDiode.Authorization.Constants;
using DataDiode.Authorization.Interfaces;
using DataDiode.Authorization.Services;
using DataDiode.Models.Token;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DataDiode.Authentication;

/// <summary>
/// Sign-in through the dashboard. Three schemes live side by side behind one policy scheme "DataDiodeAuth":
///   "Bearer"           - DataDiode's own login (Authorization: Bearer ...).
///   "Dashboard"        - the dashboard's JWT, read from the dashboard cookie (AccessToken inside its JSON),
///                        verified with YOUR validation parameters, then checked against the dashboard's AccessTokenSessions table.
///   "DashboardSession" - the dashboard's opaque refresh cookie, looked up in the AccessTokenSessions table.
/// "Auth:Enabled" in appsettings.json switches the dashboard schemes on.
/// </summary>
public static class DashboardAuthExtensions
{
    public const string PolicyScheme = "DataDiodeAuth";
    public const string OwnScheme = "Bearer";
    public const string DashboardScheme = "Dashboard";
    public const string SessionScheme = "DashboardSession";
    public const string RequestHeader = "X-DataDiode-Request";

    public static IServiceCollection AddDataDiodeAuth(this IServiceCollection services, IConfiguration config)
    {
        var enabled = config.GetValue<bool>(AppSettings.AuthEnabled);
        var cookieName = config[AppSettings.AuthCookieName] ?? "learny_token";
        var ownKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config[AppSettings.JwtKey]
            ?? throw new InvalidOperationException("Jwt:Key missing")));

        services.AddHttpContextAccessor();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserResolver, UserResolver>();
        services.AddSingleton<TokenService>();
        services.AddDbContext<AccessGateSessionsContext>(o => o.UseSqlServer(
            string.IsNullOrWhiteSpace(config.GetConnectionString(AppSettings.ConnectionExternalToken)) ? "Server=.;Database=NotConfigured" : config.GetConnectionString(AppSettings.ConnectionExternalToken)));
        services.AddScoped<ITokenValidatorService, TokenValidatorService>();
        services.AddScoped<IClaimsTransformation, DataDiodeClaimsTransformation>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        services.AddAuthentication(o =>
            {
                o.DefaultScheme = PolicyScheme;
                o.DefaultAuthenticateScheme = PolicyScheme;
                o.DefaultChallengeScheme = PolicyScheme;
            })
            .AddPolicyScheme(PolicyScheme, PolicyScheme, o =>
            {
                o.ForwardDefaultSelector = ctx =>
                {
                    var hasBearer = ctx.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
                    if (!enabled || hasBearer) return OwnScheme;
                    // the cookie holds a JWT (or JSON with one): verify it. Otherwise it is the dashboard's opaque refresh token: look the session up.
                    if (ctx.Request.Cookies.TryGetValue(cookieName, out var raw) && DashboardTokenCookie.TryRead(raw, out _)) return DashboardScheme;
                    return DashboardSessionHandler.OpaqueCookieValue(ctx.Request, config) != null ? SessionScheme : OwnScheme;
                };
            })
            .AddJwtBearer(OwnScheme, o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config[AppSettings.JwtIssuer],
                    ValidAudience = config[AppSettings.JwtAudience],
                    IssuerSigningKey = ownKey,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            })
            .AddScheme<AuthenticationSchemeOptions, DashboardSessionHandler>(SessionScheme, _ => { })
            .AddJwtBearer(DashboardScheme, o =>
            {
                // =====================================================================================================
                //  >>> YOUR VERIFICATION GOES HERE <<<
                //  Replace the body of DashboardTokenValidation with the dashboard's own token verification (key, issuer,
                //  audience, lifetime...). By default it reads Auth:Jwt:Key / Issuer / Audience from appsettings.json.
                // =====================================================================================================
                o.TokenValidationParameters = DashboardTokenValidation(config);
                o.MapInboundClaims = false;      // keep the claim names exactly as the dashboard wrote them ("sub", "email", "jti"...)

                o.Events = new JwtBearerEvents
                {
                    // the JWT is inside the JSON of the cookie
                    OnMessageReceived = ctx =>
                    {
                        if (ctx.Request.Cookies.TryGetValue(cookieName, out var raw) && DashboardTokenCookie.TryRead(raw, out var cookie) && cookie != null)
                        {
                            ctx.Token = cookie.AccessToken;
                            ctx.HttpContext.Items["datadiode:cookieUser"] = cookie.User;
                            ctx.HttpContext.Items["datadiode:fromCookie"] = true;
                        }
                        return Task.CompletedTask;
                    },
                    // verified token: it must still be a live session in the dashboard's token table
                    OnTokenValidated = async ctx =>
                    {
                        var jti = ctx.Principal?.FindFirst("jti")?.Value;
                        var sub = ctx.Principal?.FindFirst("sub")?.Value ?? ctx.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var cookieUser = ctx.HttpContext.Items["datadiode:cookieUser"] as DashboardUser;
                        var userGuid = Guid.TryParse(sub, out var g) ? g : cookieUser?.Id;
                        if (!userGuid.HasValue || string.IsNullOrEmpty(jti))
                        {
                            ctx.Fail("The token has no user id or jti.");
                            return;
                        }
                        var log = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("DataDiodeAuth");
                        try
                        {
                            var sessions = ctx.HttpContext.RequestServices.GetRequiredService<ITokenValidatorService>();
                            var session = await sessions.GetAccessTokenSessionAsync(userGuid.Value.ToString(), jti);
                            if (session == null) { log.LogWarning("No AccessTokenSessions row for jti {Jti} and user {User}.", jti, userGuid); ctx.Fail("The session was not found."); }
                            else if (session.RevokedAt != null) ctx.Fail("The session was revoked.");
                            else if (session.ExpiresAt <= DateTime.UtcNow) { log.LogWarning("Session expired at {At} (UTC now {Now}).", session.ExpiresAt, DateTime.UtcNow); ctx.Fail("The session has expired."); }
                        }
                        catch (Exception ex)
                        {
                            log.LogError(ex, "The token session check failed (is ConnectionStrings:ExternalToken right?); the request is refused.");
                            ctx.Fail("The session check failed.");
                        }
                    }
                };
            });

        services.AddAuthorization(o =>
        {
            o.AddPolicy(AuthPolicies.AdminOnly, p => p.RequireRole(AuthRoles.Admin));
            o.AddPolicy(AuthPolicies.UserOnly, p => p.RequireRole(AuthRoles.User));
        });
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<IRoleAuthorizationService, RoleAuthorizationService>();

        return services;
    }

    /// <summary>The dashboard JWT's validation rules. REPLACE with your own verification.</summary>
    private static TokenValidationParameters DashboardTokenValidation(IConfiguration config)
    {
        var key = config[AppSettings.AuthJwtKey];
        return new TokenValidationParameters
        {
            ValidateIssuer = !string.IsNullOrEmpty(config[AppSettings.AuthJwtIssuer]),
            ValidateAudience = !string.IsNullOrEmpty(config[AppSettings.AuthJwtAudience]),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config[AppSettings.AuthJwtIssuer],
            ValidAudience = config[AppSettings.AuthJwtAudience],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(string.IsNullOrEmpty(key) ? "not-configured-" + Guid.NewGuid().ToString("N") : key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// A request authenticated by the cookie could be sent by another web site that the browser lets use the cookie.
    /// So every request that CHANGES something must also carry the header "X-DataDiode-Request: 1", which only DataDiode's own pages add.
    /// </summary>
    public static IApplicationBuilder UseCookieRequestGuard(this IApplicationBuilder app)
    {
        return app.Use(async (ctx, next) =>
        {
            var safe = HttpMethods.IsGet(ctx.Request.Method) || HttpMethods.IsHead(ctx.Request.Method) || HttpMethods.IsOptions(ctx.Request.Method);
            if (!safe && ctx.Items.ContainsKey("datadiode:fromCookie") && ctx.Request.Headers[RequestHeader] != "1")
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("{\"message\":\"Missing request header.\"}");
                return;
            }
            await next();
        });
    }
}
