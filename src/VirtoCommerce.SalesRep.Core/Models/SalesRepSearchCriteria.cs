using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepSearchCriteria : SearchCriteriaBase
{
    public string OrganizationId { get; set; }

    public bool OnlyBlocked { get; set; }

    public bool OnlyUnassigned { get; set; }
}
