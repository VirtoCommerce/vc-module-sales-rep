using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.SalesRep.Core.Caching;

/// <summary>
/// One statistics cache family: a group of aggregates sharing a lifetime and an invalidation policy. The name also
/// namespaces the family's per-organization invalidation tokens, so a change concerning one family leaves the other
/// families' entries alone — which is what keeps each family's own flag meaningful.
/// </summary>
public sealed record StatisticsCacheFamily(string Name, SettingDescriptor Expiration, SettingDescriptor InvalidateOnChange);
