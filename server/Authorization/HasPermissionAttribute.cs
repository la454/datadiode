using Microsoft.AspNetCore.Authorization;

namespace DataDiode.Authorization;

/// <summary>[HasPermission("users.manage")] - the signed-in user must have that permission through at least one of their roles.</summary>
public class HasPermissionAttribute : AuthorizeAttribute
{
    public const string Prefix = "Permission:";

    public HasPermissionAttribute(string code)
    {
        Policy = Prefix + code;
    }
}
