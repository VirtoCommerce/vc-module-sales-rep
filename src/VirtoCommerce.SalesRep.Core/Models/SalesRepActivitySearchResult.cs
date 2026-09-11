using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepActivitySearchResult : GenericSearchResult<SalesRepActivityEvent>
{
    public IList<SalesRepActivityCategoryCount> CategoryCounts { get; set; } = [];

    // Whether the tracked categories are measured at all for this store. Zero searches means "none this
    // period" only when this is true; when it is false the tracked counts are absences of measurement, and
    // a reader told otherwise concludes the customer went quiet.
    public bool IsAnalyticsConfigured { get; set; }
}
