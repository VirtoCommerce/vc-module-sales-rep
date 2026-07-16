using System;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Criteria for a single cart/project-statistics bucket: the carts of one organization (or all the rep's served
/// organizations), optionally scoped to a store, a set of cart types and/or a set of cart statuses, whose created
/// date falls in [<see cref="FromDate"/>, <see cref="ToDate"/>), aggregated and converted to <see cref="CurrencyCode"/>.
/// Soft-deleted carts are always excluded. The type/status sets come from a resolved "cart kind" (e.g. "project" →
/// type "Wishlist"); the query layer maps business kind names to these underlying filters.
/// </summary>
public class CustomerCartStatisticsCriteria
{
    /// <summary>
    /// Organizations (customers) whose carts are aggregated. Which organizations the caller may see is enforced
    /// upstream (in the query handler); an empty/null set aggregates nothing.
    /// </summary>
    public string[] OrganizationIds { get; set; }

    /// <summary>
    /// Only carts created by this user are counted — the sales rep's own security-account id (a rep creates a
    /// project/cart <em>for</em> a customer, so the cart's <c>CustomerId</c> is the rep's user id). The
    /// creator-scoping half of the module's data-isolation invariant; the query handler always sets it to the caller.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>Optional store to scope the carts to. Null aggregates across all stores.</summary>
    public string StoreId { get; set; }

    /// <summary>Currency all monetary figures are converted to (cart totals are stored per cart currency).</summary>
    public string CurrencyCode { get; set; }

    /// <summary>Optional cart-type whitelist (e.g. "Wishlist"). Null/empty counts every cart type.</summary>
    public string[] Types { get; set; }

    /// <summary>Optional cart-status whitelist. Null/empty counts every status.</summary>
    public string[] Statuses { get; set; }

    /// <summary>Inclusive lower bound on the cart created date. Null = no lower bound (e.g. lifetime).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Exclusive upper bound on the cart created date. Null = no upper bound.</summary>
    public DateTime? ToDate { get; set; }
}
