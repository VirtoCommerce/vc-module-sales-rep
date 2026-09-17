using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepActivitySearchResult : GenericSearchResult<SalesRepActivityEvent>
{
    public IList<SalesRepActivityCategoryCount> CategoryCounts { get; set; } = [];

    // Whether the tracked categories are measured at all for this store — false for an absent or unconfigured
    // analytics module and for a read that failed alike. Zero searches means "none this period" only when
    // this is true; otherwise the counts are absences of measurement, and a reader told otherwise concludes
    // the customer went quiet. Defaults true, so only a source that actually failed turns it off.
    public bool IsAnalyticsAvailable { get; set; } = true;
}
