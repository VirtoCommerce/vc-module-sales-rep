using System;
using System.Linq;
using System.Security.Claims;
using VirtoCommerce.Platform.Core;

namespace VirtoCommerce.SalesRep.Core;

// Single source of truth for the documents-library permission check, shared by the Data authorization handler
// and the ExperienceApi resolver guard. A limited_permissions claim restricts the effective set; otherwise the
// global permission claims apply.
public static class SalesRepDocumentPermissions
{
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
