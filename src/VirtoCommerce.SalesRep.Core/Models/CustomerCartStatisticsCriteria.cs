using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class CustomerCartStatisticsCriteria : ValueObject
{
    public IList<string> OrganizationIds { get; set; }

    public string CustomerId { get; set; }

    public string StoreId { get; set; }

    public string CurrencyCode { get; set; }

    public IList<string> Names { get; set; }

    public IList<string> Types { get; set; }

    public IList<string> ExcludeTypes { get; set; }

    public IList<string> Statuses { get; set; }

    // Carried for the filter-rule contract, deliberately not applied to the query: every figure here is
    // aggregated from the line items, so a cart with none contributes nothing anyway, and the denormalized
    // Cart.LineItemsCount it would filter on can be stale enough to drop a cart that does have items.
    public bool OnlyNonEmpty { get; set; }

    public CartStatisticsResponseGroup ResponseGroup { get; set; } = CartStatisticsResponseGroup.Full;

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}
