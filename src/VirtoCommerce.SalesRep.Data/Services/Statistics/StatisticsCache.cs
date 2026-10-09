using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Caching;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Data.Caching;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

internal static class StatisticsCache
{
    private static readonly TimeSpan CacheDisabled = TimeSpan.FromTicks(1);

    public static Task<T> GetOrCreateAsync<T>(
        IPlatformMemoryCache platformMemoryCache,
        ISettingsManager settingsManager,
        StatisticsCacheFamily family,
        Type ownerType,
        string method,
        IStatisticsCacheCriteria criteria,
        Func<Task<T>> factory)
    {
        var key = CacheKey.With(ownerType, method, criteria.GetCacheKey());
        return platformMemoryCache.GetOrCreateExclusiveAsync(key, async options =>
        {
            var minutes = await settingsManager.GetValueAsync<int>(family.Expiration);

            if (Apply(options, TimeSpan.FromMinutes(minutes)) &&
                await StatisticsCacheInvalidation.IsEnabledAsync(settingsManager, family))
            {
                // Before the aggregation, not after: cancelling a token removes its source, so one created after a
                // racing invalidation comes back live and caches the value that change invalidated.
                options.AddExpirationToken(SalesRepStatisticsCacheRegion.CreateChangeToken(family, criteria.OrganizationIds));
            }

            return await factory();
        });
    }

    // False when the entry will not be cached at all, so nothing should be attached to it.
    private static bool Apply(MemoryCacheEntryOptions options, TimeSpan ttl)
    {
        if (options.AbsoluteExpirationRelativeToNow == CacheDisabled)
        {
            return false;
        }

        if (ttl <= TimeSpan.Zero)
        {
            options.AbsoluteExpirationRelativeToNow = CacheDisabled;
            return false;
        }

        options.AbsoluteExpirationRelativeToNow = ttl;

        // The platform's default options may carry a sliding window, which would silently cap the configured
        // lifetime at min(ttl, idle time). This setting is the whole lifetime.
        options.SlidingExpiration = null;

        return true;
    }
}
