using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepTopSellerCriteria : ValueObject, IStatisticsCacheCriteria
{
    public IList<string> OrganizationIds { get; set; }

    public string CustomerId { get; set; }

    public string StoreId { get; set; }

    public string CurrencyCode { get; set; }

    public IList<string> CategoryIds { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public SalesRepTopSellerSortBy SortBy { get; set; }

    public const int DefaultTake = 5;

    public int Take { get; set; } = DefaultTake;
}
