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

    /// <summary>
    /// Whether to aggregate the cart-level figures (count, total, average) on top of the item quantities. They cost
    /// a COUNT DISTINCT and a currency conversion, so the resolver only asks for them when the caller selected one.
    /// Part of the cache key (a <see cref="ValueObject"/> keys on every property), so a quantities-only result can
    /// never be served to a request that wants the money.
    /// </summary>
    public bool IncludeCartFigures { get; set; }

    /// <summary>
    /// Inclusive lower bound on each line item's modified date. The cart's own dates are never filtered — a cart
    /// opened months ago still reports the items touched inside the range.
    /// </summary>
    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}
