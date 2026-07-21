using VirtoCommerce.Platform.Core.Caching;

namespace VirtoCommerce.SalesRep.Data.Services.Statistics;

/// <summary>
/// Cache region for the sales-rep dashboard statistics aggregates (order / cart / customer-count widgets, VCST-5309).
/// Entries expire on their own time-based TTL (see the module's <c>SalesRep.Statistics.*CacheExpirationMinutes</c>
/// settings); this region is intentionally NOT expired by any CRUD event, so a new order/cart/member does not flush
/// the widgets — staleness is bounded by the TTL by design, which is what keeps the cache useful on a busy store.
/// It exists only as a manual-flush handle: call <see cref="CancellableCacheRegion.CancelForKey"/>-style
/// <c>StatisticsCacheRegion.ExpireRegion()</c> to drop every cached statistics aggregate at once.
/// </summary>
public class StatisticsCacheRegion : CancellableCacheRegion<StatisticsCacheRegion>
{
}
