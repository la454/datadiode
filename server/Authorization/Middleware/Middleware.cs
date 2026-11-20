using System.Text.Json;
using DataDiode.Authorization.Constants;
using DataDiode.Authorization.Interfaces;

namespace DataDiode.Authorization.Middleware;

/// <summary>BusinessException -> clean JSON { message } with the right status code.</summary>
public class BusinessExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public BusinessExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        BusinessException? failure = null;
        try
        {
            await _next(ctx);
        }
        catch (BusinessException ex)
        {
            failure = ex;
        }
        if (failure != null && !ctx.Response.HasStarted)
        {
            ctx.Response.Clear();
            ctx.Response.StatusCode = failure.StatusCode;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { message = failure.Message }, Camel));
        }
    }
}

/// <summary>Turns an empty 401/403 from the auth pipeline into a JSON { message } body, like BusinessException responses.</summary>
public class ForbiddenResponseMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly JsonSerializerOptions Camel = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ForbiddenResponseMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        await _next(ctx);

        var status = ctx.Response.StatusCode;
        if ((status == StatusCodes.Status401Unauthorized || status == StatusCodes.Status403Forbidden)
            && !ctx.Response.HasStarted && ctx.Response.ContentLength == null && string.IsNullOrEmpty(ctx.Response.ContentType))
        {
            ctx.Response.ContentType = "application/json";
            var message = status == StatusCodes.Status401Unauthorized
                ? "You must sign in first."
                : "You do not have permission to access this resource.";
            await ctx.Response.WriteAsync(JsonSerializer.Serialize(new { message }, Camel));
        }
    }
}

/// <summary>Writes every refused API call (401 / 403) to the audit log.</summary>
public class UnauthorizedAccessAuditMiddleware
{
    private readonly RequestDelegate _next;

    public UnauthorizedAccessAuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAuditService audit, IIdentityService identity)
    {
        await _next(context);

        var status = context.Response.StatusCode;
        // the page's "am I signed in?" probe is not an attack: a 401 there is the normal answer for a visitor, so it is not logged
        var probe = status == 401 && HttpMethods.IsGet(context.Request.Method) && context.Request.Path.Equals("/api/auth/me", StringComparison.OrdinalIgnoreCase);
        if ((status == 401 || status == 403) && !probe && context.Request.Path.StartsWithSegments("/api"))
        {
            var path = context.Request.Path.ToString();
            var method = context.Request.Method;
            await audit.LogAsync(
                status == 401 ? AuditActions.UnauthorizedAccess : AuditActions.ForbiddenAccess,
                identity.GetCurrentUserId(), identity.GetCurrentUserName(), "API", method + " " + path,
                $"{method} {path} answered {status}", false,
                status == 401 ? "Not authenticated" : "Insufficient permissions",
                context.Connection.RemoteIpAddress?.ToString(), context.Request.Headers.UserAgent.ToString());
        }
    }
}
