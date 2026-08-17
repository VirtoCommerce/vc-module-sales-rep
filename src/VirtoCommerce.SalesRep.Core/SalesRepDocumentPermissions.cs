using System;
using System.Linq;
using System.Security.Claims;
using VirtoCommerce.Platform.Core;

namespace VirtoCommerce.SalesRep.Core;

// Single source of truth for the documents-library access checks, shared by the REST controller and the
// ExperienceApi resolver guard. Fail-closed everywhere: reads require documents:read OR documents:write
// (write implies read) OR the Administrator role; writes require documents:write OR Administrator; anonymous
// never passes. A limited_permissions claim restricts the effective permission set; otherwise the global
// permission claims apply.
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
