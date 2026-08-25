using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepActivitySearchResult : GenericSearchResult<SalesRepActivityEvent>
{
    public IList<SalesRepActivityCategoryCount> CategoryCounts { get; set; } = [];
}
