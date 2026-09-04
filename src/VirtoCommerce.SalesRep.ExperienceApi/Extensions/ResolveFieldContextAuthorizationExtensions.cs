using System;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Security.Authorization;
using VirtoCommerce.Xapi.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class ResolveFieldContextAuthorizationExtensions
{
    // Claims cannot see that the account behind a still-valid token was locked, deleted or expired.
    public static async Task EnsureAuthenticatedAsync(this IResolveFieldContext context)
    {
        // Fast path: CheckCurrentUserState reaches the same refusal, but only after a user lookup.
        if (!context.IsAuthenticated())
        {
            throw AuthorizationError.AnonymousAccessDenied();
        }

        await context.GetRequiredService<IUserManagerCore>().CheckCurrentUserState(context, allowAnonymous: false);
    }
}
