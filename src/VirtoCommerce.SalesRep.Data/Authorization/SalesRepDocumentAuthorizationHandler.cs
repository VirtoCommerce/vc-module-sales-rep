using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Security.Authorization;
using VirtoCommerce.SalesRep.Core;

namespace VirtoCommerce.SalesRep.Data.Authorization;

// Fail-closed gate for the shared documents library: an authenticated caller needs documents:read for reads
// and documents:write for mutations; write implies read; platform Administrator always passes; anonymous never
// does. Unlike the default file-exp-api handler, there is NO ownerless-file shortcut.
public class SalesRepDocumentAuthorizationHandler : PermissionAuthorizationHandlerBase<SalesRepDocumentAuthorizationRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, SalesRepDocumentAuthorizationRequirement requirement)
    {
        var authorized = context.User.Identity?.IsAuthenticated == true &&
            (context.User.IsInRole(PlatformConstants.Security.SystemRoles.Administrator) ||
             CheckPermission(context.User, requirement.Permission) ||
             CheckPermission(context.User, ModuleConstants.Security.Permissions.DocumentsWrite));

        if (authorized)
        {
            context.Succeed(requirement);
        }
        else
        {
            context.Fail();
        }

        return Task.CompletedTask;
    }

    // Mirrors PermissionAuthorizationHandlerBase: a limited_permissions claim restricts the effective set;
    // otherwise the global permission claims apply.
    protected virtual bool CheckPermission(ClaimsPrincipal user, string permission)
    {
        var limitedPermissionsClaim = user.FindFirstValue(PlatformConstants.Security.Claims.LimitedPermissionsClaimType);

        if (limitedPermissionsClaim != null)
        {
            var limitedPermissions = limitedPermissionsClaim.Split(PlatformConstants.Security.Claims.PermissionClaimTypeDelimiter, StringSplitOptions.RemoveEmptyEntries);

            return limitedPermissions.Contains(permission);
        }

        return user.HasClaim(PlatformConstants.Security.Claims.PermissionClaimType, permission);
    }
}
