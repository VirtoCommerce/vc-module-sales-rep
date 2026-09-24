using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Primitives;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.SalesRep.Core.Caching;

namespace VirtoCommerce.SalesRep.Data.Caching;

// Keyed by (family, organization): every cached variant of an organization — periods, filters, currencies, reps —
// dies together, which is what makes the hub totals agree with the sum of their own customer cards.
public class SalesRepStatisticsCacheRegion : CancellableCacheRegion<SalesRepStatisticsCacheRegion>
{
    // No organizations (an unscoped criteria, which the services accept) yields the region token alone.
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
