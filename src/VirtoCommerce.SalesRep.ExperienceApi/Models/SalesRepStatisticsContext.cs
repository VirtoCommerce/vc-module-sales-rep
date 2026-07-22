using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public abstract class SalesRepStatisticsContext
{
    public IList<string> OrganizationIds { get; set; }

    public string SalesRepUserId { get; set; }

    public string StoreId { get; set; }
}
