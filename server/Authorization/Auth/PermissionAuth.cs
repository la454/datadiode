using System.Security.Claims;
using DataDiode.Authorization.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DataDiode.Authorization.Auth;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string PermissionName { get; }
    public PermissionRequirement(string permissionName) => PermissionName = permissionName;
}

public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IPermissionService _permissionService;

    public PermissionHandler(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // The permissions are NOT read from the token: they are always fetched from the DataDiode database (cached for the request).
        // After DataDiodeClaimsTransformation the NameIdentifier claim is the DataDiode user id (an int), for dashboard users too.
        var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userIdClaim == null || !int.TryParse(userIdClaim, out var userId))
        {
            context.Fail();     // no DataDiode identity (for example a dashboard user unknown or disabled here)
            return;
        }

        if (await _permissionService.HasAsync(userId, requirement.PermissionName))
            context.Succeed(requirement);
        else
            context.Fail();     // explicit, for easier debugging
    }
}

// Builds policies on the fly for "Permission:XYZ" policy names ([HasPermission("XYZ")] produces that name).
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    const string POLICY_PREFIX = HasPermissionAttribute.Prefix;
    public DefaultAuthorizationPolicyProvider FallbackPolicyProvider { get; }

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        FallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(POLICY_PREFIX, StringComparison.OrdinalIgnoreCase))
        {
            var permissionName = policyName.Substring(POLICY_PREFIX.Length);
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(permissionName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return FallbackPolicyProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => FallbackPolicyProvider.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => FallbackPolicyProvider.GetFallbackPolicyAsync();
}
