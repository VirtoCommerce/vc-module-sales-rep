using GraphQL;
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
}
