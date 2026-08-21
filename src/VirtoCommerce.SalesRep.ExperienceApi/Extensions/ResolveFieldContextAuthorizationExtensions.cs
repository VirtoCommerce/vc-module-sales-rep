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
    /// <summary>
    /// The gate every sales-rep query and mutation passes through: the caller is signed in, and the account
    /// behind the token is still usable. The claims alone do not answer the second half — a token stays valid
    /// for its full lifetime after the account is locked, deleted or its password expires, and the module's
    /// membership scoping does not cover it either (OrganizationMembership.IsLocked is the membership, not the
    /// account). IUserManagerCore is resolved per request the way Xapi resolves the mediator, so no builder has
    /// to carry it through its constructor.
    /// </summary>
    public static async Task EnsureAuthenticatedAsync(this IResolveFieldContext context)
    {
        if (!context.IsAuthenticated())
        {
            throw AuthorizationError.AnonymousAccessDenied();
        }

        if (context.RequestServices == null)
        {
            throw new InvalidOperationException(
                "Cannot verify the caller's account state: IResolveFieldContext.RequestServices is null. " +
                "The GraphQL HTTP middleware populates it - in tests, set ExecutionOptions.RequestServices explicitly.");
        }

        var userManagerCore = context.RequestServices.GetRequiredService<IUserManagerCore>();

        await userManagerCore.CheckCurrentUserState(context, allowAnonymous: false);
    }
}
