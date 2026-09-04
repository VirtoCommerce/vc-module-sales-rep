using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Caching;

namespace VirtoCommerce.SalesRep.Data.Caching;

/// <summary>
/// The one place that decides whether a family invalidates on change. Both sides of the mechanism ask it — entry
/// creation (attach the organization tokens?) and the event handlers (cancel them?) — so a flag flipped in the admin
/// takes effect without a redeploy, and the four families stay data rather than four code paths.
/// </summary>
internal static class StatisticsCacheInvalidation
{
    public static async Task<bool> IsEnabledAsync(ISettingsManager settingsManager, StatisticsCacheFamily family)
    {
        // A family whose cache is off holds no entries to evict, so the flag alone can never switch invalidation on —
        // otherwise every save would broadcast expirations cluster-wide for a cache that stores nothing.
        var minutes = await settingsManager.GetValueAsync<int>(family.Expiration);

        return minutes > 0 && await settingsManager.GetValueAsync<bool>(family.InvalidateOnChange);
    }

    public static async Task ExpireAsync(
        ISettingsManager settingsManager,
        IEnumerable<StatisticsCacheFamily> families,
        IList<string> organizationIds)
    {
        if (organizationIds.Count == 0)
        {
            return;
        }

        foreach (var family in families)
        {
            if (!await IsEnabledAsync(settingsManager, family))
            {
                continue;
            }

            foreach (var organizationId in organizationIds)
            {
                SalesRepStatisticsCacheRegion.ExpireOrganization(family, organizationId);
            }
        }
    }
}
