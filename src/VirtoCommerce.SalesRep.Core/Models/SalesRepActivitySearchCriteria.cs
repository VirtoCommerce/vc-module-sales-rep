using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepActivitySearchCriteria : SearchCriteriaBase
{
    // SearchCriteriaBase's own page size, named so the query's default chains to it.
    public const int DefaultTake = 20;

    public string SalesRepUserId { get; set; }

    public IList<string> OrganizationIds { get; set; } = [];

    public IList<string> Categories { get; set; }

    public string StoreId { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    // The aggregator mutates Categories on each clone before firing the per-category reads concurrently,
    // so a clone must not share the collections with the original.
    public override object Clone()
    {
        var result = (SalesRepActivitySearchCriteria)base.Clone();

        result.OrganizationIds = OrganizationIds?.ToList();
        result.Categories = Categories?.ToList();

        return result;
    }
}
