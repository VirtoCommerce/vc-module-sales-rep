using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepDocumentMetadataSearchCriteria : SearchCriteriaBase
{
    public string Category { get; set; }

    public bool? IsPinned { get; set; }
}
