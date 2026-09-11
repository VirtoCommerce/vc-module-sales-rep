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

    // Counting the categories the caller did NOT ask for is what makes a request cost an analytics read it has
    // no use for: the Orders tab would wait on Google to fill in badges nobody selected. False plans only the
    // requested categories, so a caller that skips the badges pays for its own rows and nothing else.
    public bool IncludeCategoryCounts { get; set; } = true;

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
