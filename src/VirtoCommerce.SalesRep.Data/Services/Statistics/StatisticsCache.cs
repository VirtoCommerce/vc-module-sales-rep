using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

internal static class StatisticsCache
{
    private static readonly TimeSpan CacheDisabled = TimeSpan.FromTicks(1);

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
