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
    /// <param name="organizationIds">
    /// The organizations the entry aggregates. An unscoped criteria (no organizations — which the statistics services
    /// accept) yields the region token alone, so such an entry rides its TTL and region-wide expiry instead of failing.
    /// </param>
    public static IChangeToken CreateChangeToken(StatisticsCacheFamily family, IList<string> organizationIds)
    {
        var changeTokens = new List<IChangeToken> { CreateChangeToken() };
        changeTokens.AddRange((organizationIds ?? [])
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(x => CreateChangeTokenForKey(GetTokenKey(family, x))));

        return new CompositeChangeToken(changeTokens);
    }

    public static void ExpireOrganization(StatisticsCacheFamily family, string organizationId)
    {
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
