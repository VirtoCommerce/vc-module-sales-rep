using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepActivitySearchResult : GenericSearchResult<SalesRepActivityEvent>
{
    public IList<SalesRepActivityCategoryCount> CategoryCounts { get; set; } = [];

    // The events the rows stand for — an analytics row is an hour bucket that can hold several. Null when that is the
    // row count, as for every platform row. Badges count events; the pager pages rows.
    public int? EventCount { get; set; }

    // Defaults TRUE: every source returns one of these, so only a source that failed turns it off.
    public bool IsAnalyticsAvailable { get; set; } = true;
}
