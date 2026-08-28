using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepActivitySource
{
    IList<string> Categories { get; }

    // ISalesRepActivityService calls this once per category it owns, with criteria.Categories naming exactly that
    // one — a source may answer for a single category and does not have to merge across its own. Take/Skip are
    // therefore per-category; Take = 0 means "count only". The caller owns the merge, sort and page slice.
    Task<SalesRepActivitySearchResult> SearchAsync(SalesRepActivitySearchCriteria criteria);
}
