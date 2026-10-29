using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MiniMES.Web.Security;

public static class Permissions
{
    public const string ClaimType = "FUN";
    public const string ProductView = "MODEL_VIEW";
    public const string ProductAdd = "MODEL_ADD";
    public const string ProductEdit = "MODEL_EDIT";
    public const string ProductDelete = "MODEL_DELETE";
    public const string AuditView = "AUDIT_VIEW";
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class PermissionAuthorizeAttribute : TypeFilterAttribute
{
    public PermissionAuthorizeAttribute(string functionCode) : base(typeof(PermissionFilter))
    {
        Arguments = [functionCode];
    }
}

public sealed class PermissionFilter(string functionCode) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new ChallengeResult();
            return;
        }
        if (!context.HttpContext.User.HasClaim(Permissions.ClaimType, functionCode))
            context.Result = new ForbidResult();
    }
}
