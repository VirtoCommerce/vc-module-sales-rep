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
    //
    // Those per-category calls run CONCURRENTLY against the same instance, so an implementation must be safe for
    // concurrent reentrancy — which rules out holding a scoped DbContext, the shape a module service usually has.
    // Resolve one per call instead.
    //
    // A category should belong to one source: the aggregator sums a category claimed by two, but its per-category
    // counts are keyed by category and one of the two answers would be the one a client renders.
    Task<SalesRepActivitySearchResult> SearchAsync(SalesRepActivitySearchCriteria criteria);
}
