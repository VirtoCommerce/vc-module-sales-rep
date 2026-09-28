using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Caching;

namespace VirtoCommerce.SalesRep.Data.Caching;

// The one place that decides whether a family invalidates on change; entry creation and the handlers both ask it.
internal static class StatisticsCacheInvalidation
{
    public static async Task<bool> IsEnabledAsync(ISettingsManager settingsManager, StatisticsCacheFamily family)
    {
        // A family whose cache is off holds nothing to evict, and expiring anyway would broadcast cluster-wide.
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
