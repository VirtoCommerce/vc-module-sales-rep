using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepActivitySearchCriteria : ICloneable
{
    public string SalesRepUserId { get; set; }

    public string OrganizationId { get; set; }

    public IList<string> OrganizationIds { get; set; } = [];

    public IList<string> Categories { get; set; }

    public string StoreId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Take { get; set; } = 20;

    public int Skip { get; set; }

    public string CultureName { get; set; }

    public virtual object Clone()
    {
        var result = (SalesRepActivitySearchCriteria)MemberwiseClone();
        result.OrganizationIds = OrganizationIds?.ToList();
        result.Categories = Categories?.ToList();
        return result;
    }
}
