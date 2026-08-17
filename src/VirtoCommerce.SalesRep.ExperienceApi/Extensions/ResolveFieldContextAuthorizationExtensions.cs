using GraphQL;
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

    // Shares the SalesRepDocumentPermissions predicate (.Core) with the REST controller: read requires
    // documents:read OR documents:write (write implies read) OR the Administrator role; anonymous never passes;
    // a limited_permissions claim restricts the effective permission set.
    public static void EnsureCanReadDocuments(this IResolveFieldContext context)
    {
        context.EnsureAuthenticated();

        if (!SalesRepDocumentPermissions.HasReadAccess(context.GetCurrentPrincipal()))
        {
            throw AuthorizationError.PermissionRequired(ModuleConstants.Security.Permissions.DocumentsRead);
        }
    }
}
