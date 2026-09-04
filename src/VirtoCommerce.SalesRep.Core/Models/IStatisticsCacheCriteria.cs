using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Caching;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Statistics criteria the aggregate cache can both key and scope: the key comes from <c>GetCacheKey</c>, the
/// invalidation tokens from <see cref="OrganizationIds"/>. Pairing them in one contract is what keeps an entry's key
/// and its tokens describing the same set of records.
/// </summary>
public interface IStatisticsCacheCriteria : ICacheKey
{
    IList<string> OrganizationIds { get; }
}
