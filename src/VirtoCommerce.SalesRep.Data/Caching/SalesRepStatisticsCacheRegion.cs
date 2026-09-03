using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Primitives;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.SalesRep.Core.Caching;

namespace VirtoCommerce.SalesRep.Data.Caching;

/// <summary>
/// Invalidation tokens for the statistics aggregates, keyed by (family, organization): every cached key variant of an
/// organization — periods, filters, currencies, reps — dies together, which is what makes the hub totals agree with
/// the sum of their own customer cards. Expirations propagate, so with the Redis backplane configured they reach
/// every instance; the whole region can also be dropped at once.
/// </summary>
public class SalesRepStatisticsCacheRegion : CancellableCacheRegion<SalesRepStatisticsCacheRegion>
{
    public static IChangeToken CreateChangeToken(StatisticsCacheFamily family, IList<string> organizationIds)
    {
        ArgumentNullException.ThrowIfNull(family);
        ArgumentNullException.ThrowIfNull(organizationIds);

        var changeTokens = new List<IChangeToken> { CreateChangeToken() };
        changeTokens.AddRange(organizationIds
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => CreateChangeTokenForKey(GetTokenKey(family, x))));

        return new CompositeChangeToken(changeTokens);
    }

    public static void ExpireOrganization(StatisticsCacheFamily family, string organizationId)
    {
        ArgumentNullException.ThrowIfNull(family);

        if (!string.IsNullOrEmpty(organizationId))
        {
            ExpireTokenForKey(GetTokenKey(family, organizationId));
        }
    }

    private static string GetTokenKey(StatisticsCacheFamily family, string organizationId)
    {
        return $"{family.Name}:{organizationId}";
    }
}
