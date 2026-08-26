using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepDocumentSearchCriteria : SearchCriteriaBase
{
    public string Category { get; set; }

    public bool? IsPinned { get; set; }
}
