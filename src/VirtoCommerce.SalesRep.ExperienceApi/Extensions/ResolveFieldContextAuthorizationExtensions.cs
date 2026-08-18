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

    // Shares the SalesRepDocumentPermissions read predicate (.Core) with the REST controller.
    public static void EnsureCanReadDocuments(this IResolveFieldContext context)
    {
        context.EnsureAuthenticated();

        if (!context.GetCurrentPrincipal().HasReadAccess())
        {
            throw AuthorizationError.PermissionRequired(ModuleConstants.Security.Permissions.DocumentsRead);
        }
    }
}
