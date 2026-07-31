using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Scope of the "which categories were sold into" lookup. Deliberately narrower than
/// <see cref="SalesRepTopSellerCriteria"/>: the answer depends only on the records in scope, so ranking inputs (take,
/// sort, currency) must stay out of it — they would otherwise split the cached result across keys that all hold the
/// same data.
/// </summary>
public class SalesRepSoldCategoryCriteria : ValueObject
{
    public IList<string> OrganizationIds { get; set; }

    public string CustomerId { get; set; }

    public string StoreId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}
