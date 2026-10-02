using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepActivitySearchResult : GenericSearchResult<SalesRepActivityEvent>
{
    public IList<SalesRepActivityCategoryCount> CategoryCounts { get; set; } = [];

    // Defaults TRUE: every source returns one of these, so only a source that failed turns it off.
    public bool IsAnalyticsAvailable { get; set; } = true;
}
