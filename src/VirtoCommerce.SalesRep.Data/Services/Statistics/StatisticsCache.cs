using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

/// <summary>
/// Shared helpers for the time-based caching in the sales-rep statistics services (orders, carts, customer counts).
/// All three cache their DB aggregate the same way: keyed on the full criteria, expiring purely on a per-query TTL.
/// See <see cref="Apply"/> for why no entity change token is used.
/// </summary>
internal static class StatisticsCache
{
    // PlatformMemoryCache.GetDefaultCacheEntryOptions() stamps this 1-tick expiration when caching is globally off.
    private static readonly TimeSpan CacheDisabled = TimeSpan.FromTicks(1);

    /// <summary>
    /// Configures a statistics cache entry for time-based expiration.
    /// <list type="bullet">
    /// <item>Respects the platform's global cache switch: when caching is off the default options carry a 1-tick
    /// expiration, which we leave untouched — overriding it would silently re-enable caching for these entries.</item>
    /// <item>A non-positive <paramref name="ttl"/> disables caching for this query (per-query opt-out via the module
    /// setting): the entry is expired immediately so the next call recomputes.</item>
    /// <item>Adds NO entity (order/cart/member) change token: a data change does not flush the aggregate, so a busy
    /// store keeps a useful hit rate and staleness is bounded by the TTL. The dedicated
    /// <see cref="StatisticsCacheRegion"/> token gives a manual-flush handle and rides the platform-wide reset.</item>
    /// </list>
    /// </summary>
    public static void Apply(MemoryCacheEntryOptions options, TimeSpan ttl)
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
        options.AddExpirationToken(StatisticsCacheRegion.CreateChangeToken());
    }

    /// <summary>Order-insensitive, null-safe join of a set of tokens for use inside a cache key.</summary>
    public static string Join(IEnumerable<string> values) =>
        values == null ? string.Empty : string.Join(',', values.OrderBy(x => x, StringComparer.Ordinal));
}
