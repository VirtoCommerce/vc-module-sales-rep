using System;
using GraphQL;
using Microsoft.Extensions.DependencyInjection;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class ResolveFieldContextServiceExtensions
{
    // A schema builder is constructed once, at schema build time, so a constructor-injected dependency comes
    // from the root provider and is held for the application's lifetime - which is wrong for anything scoped,
    // and only accidentally safe for anything else. Resolve per request instead, the way Xapi resolves the
    // mediator.
    public static T GetRequiredService<T>(this IResolveFieldContext context)
    {
        if (context?.RequestServices == null)
        {
            throw new InvalidOperationException(
                $"Cannot resolve {typeof(T).Name}: IResolveFieldContext.RequestServices is null. " +
                "The GraphQL HTTP middleware populates it - in tests, set ExecutionOptions.RequestServices explicitly.");
        }

        return context.RequestServices.GetRequiredService<T>();
    }
}
