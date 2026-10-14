using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Caching;

namespace VirtoCommerce.SalesRep.Core.Models;

// One contract for both halves of a cache entry, so its key and its tokens cannot describe different records.
public interface IStatisticsCacheCriteria : ICacheKey
{
    IList<string> OrganizationIds { get; }
}
