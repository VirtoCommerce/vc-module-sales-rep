using System;
using System.Linq;
using System.Security.Claims;
using VirtoCommerce.Platform.Core;

namespace VirtoCommerce.SalesRep.Core;

// Fail-closed: read = documents:read OR documents:write OR Administrator; write = documents:write OR Administrator; anonymous never passes.
public static class SalesRepDocumentPermissions
{
    public static bool HasReadAccess(ClaimsPrincipal user)
    {
        return user.Identity?.IsAuthenticated == true &&
            (user.IsInRole(PlatformConstants.Security.SystemRoles.Administrator) ||
             HasPermission(user, ModuleConstants.Security.Permissions.DocumentsRead) ||
             HasPermission(user, ModuleConstants.Security.Permissions.DocumentsWrite));
    }

    public static bool HasWriteAccess(ClaimsPrincipal user)
    {
        return user.Identity?.IsAuthenticated == true &&
            (user.IsInRole(PlatformConstants.Security.SystemRoles.Administrator) ||
             HasPermission(user, ModuleConstants.Security.Permissions.DocumentsWrite));
    }

    public static bool HasPermission(ClaimsPrincipal user, string permission)
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
