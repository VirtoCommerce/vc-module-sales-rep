using GraphQL;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Security.Authorization;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class ResolveFieldContextAuthorizationExtensions
{
    /// <summary>
    /// Enforces the module-wide access rule shared by every Sales Rep query builder: reject an anonymous caller.
    /// (The caller must be an authenticated Sales Rep; whether they actually serve any customer is enforced
    /// per-query in the handlers.) Single-sources the guard so the two base builders — which extend different Xapi
    /// bases and so can't share one — can't drift on it.
    /// </summary>
    public static void EnsureAuthenticated(this IResolveFieldContext context)
    {
        if (!context.IsAuthenticated())
        {
            throw AuthorizationError.AnonymousAccessDenied();
        }
    }
}
