using System;
using System.Linq;
using System.Security.Claims;
using GraphQL;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Security.Authorization;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class ResolveFieldContextAuthorizationExtensions
{
    public static void EnsureAuthenticated(this IResolveFieldContext context)
    {
        if (!context.IsAuthenticated())
        {
            throw AuthorizationError.AnonymousAccessDenied();
        }
    }

    // Claim-level mirror of SalesRepDocumentAuthorizationHandler (Data — not referenceable from this project):
    // read requires documents:read OR documents:write (write implies read) OR the Administrator role; anonymous
    // never passes; a limited_permissions claim restricts the effective permission set.
    public static void EnsureCanReadDocuments(this IResolveFieldContext context)
    {
        context.EnsureAuthenticated();

        var user = context.GetCurrentPrincipal();
        var authorized = user.IsInRole(PlatformConstants.Security.SystemRoles.Administrator) ||
            HasPermission(user, ModuleConstants.Security.Permissions.DocumentsRead) ||
            HasPermission(user, ModuleConstants.Security.Permissions.DocumentsWrite);

        if (!authorized)
        {
            throw AuthorizationError.PermissionRequired(ModuleConstants.Security.Permissions.DocumentsRead);
        }
    }

    private static bool HasPermission(ClaimsPrincipal user, string permission)
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
