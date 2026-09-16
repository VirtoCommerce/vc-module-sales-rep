using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.SalesRep.Core.Caching;

// The name namespaces the family's tokens, so one family's change leaves the others' entries alone.
public sealed record StatisticsCacheFamily(string Name, SettingDescriptor Expiration, SettingDescriptor InvalidateOnChange);
