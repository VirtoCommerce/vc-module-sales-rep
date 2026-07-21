using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

/// <summary>
/// Shared time-based caching for the sales-rep dashboard aggregate services (orders, carts, customer counts, top
/// sellers). Every aggregate is cached the same way — keyed on the full criteria, expiring purely on a per-query TTL
/// (a module setting, in minutes) with no entity change token, so a data change does not flush the widget and
/// staleness is bounded by the TTL. <see cref="GetOrCreateAsync"/> is the single entry point.
/// </summary>
internal static class StatisticsCache
{
    // PlatformMemoryCache.GetDefaultCacheEntryOptions() stamps this 1-tick expiration when caching is globally off.
    private static readonly TimeSpan CacheDisabled = TimeSpan.FromTicks(1);

    /// <summary>
    /// Runs <paramref name="factory"/> behind the shared statistics cache. The entry key is
    /// <c>CacheKey.With(ownerType, method, cacheKey)</c> — <paramref name="ownerType"/> is the concrete service type
    /// (so an overriding subclass gets its own namespace) and <paramref name="cacheKey"/> is the criteria's
    /// <c>GetCacheKey()</c>. The TTL setting read and the factory both run only on a cache miss.
    /// </summary>
    public static Task<T> GetOrCreateAsync<T>(
        IPlatformMemoryCache platformMemoryCache,
        ISettingsManager settingsManager,
        SettingDescriptor ttlSetting,
        Type ownerType,
        string method,
        string cacheKey,
        Func<Task<T>> factory)
    {
        var key = CacheKey.With(ownerType, method, cacheKey);
        return platformMemoryCache.GetOrCreateExclusiveAsync(key, async options =>
        {
            var minutes = await settingsManager.GetValueAsync<int>(ttlSetting);
            Apply(options, TimeSpan.FromMinutes(minutes));
            return await factory();
        });
    }

    /// <summary>
    /// Configures a statistics cache entry for time-based expiration.
    /// <list type="bullet">
    /// <item>Respects the platform's global cache switch: when caching is off the default options carry a 1-tick
    /// expiration, which we leave untouched — overriding it would silently re-enable caching for these entries.</item>
    /// <item>A non-positive <paramref name="ttl"/> disables caching for this query (per-query opt-out via the module
    /// setting): the entry is expired immediately so the next call recomputes.</item>
    /// <item>Adds NO entity (order/cart/member) change token: a data change does not flush the aggregate, so a busy
    /// store keeps a useful hit rate and staleness is bounded by the TTL.</item>
    /// </list>
    /// </summary>
    private static void Apply(MemoryCacheEntryOptions options, TimeSpan ttl)
    {
        if (options.AbsoluteExpirationRelativeToNow == CacheDisabled)
        {
            return;
        }

        if (ttl <= TimeSpan.Zero)
        {
            options.AbsoluteExpirationRelativeToNow = CacheDisabled;
            return;
        }

        options.AbsoluteExpirationRelativeToNow = ttl;
    }
}
