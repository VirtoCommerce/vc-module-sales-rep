using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepActivitySource
{
    IList<string> Categories { get; }

    // Called once per owned category (criteria.Categories names exactly that one), so Take/Skip are per-category
    // and Take = 0 means "count only"; the caller merges, sorts and pages. The calls run CONCURRENTLY on one
    // instance: no scoped DbContext. A category claimed by two sources is summed.
    Task<SalesRepActivitySearchResult> SearchAsync(SalesRepActivitySearchCriteria criteria);
}
