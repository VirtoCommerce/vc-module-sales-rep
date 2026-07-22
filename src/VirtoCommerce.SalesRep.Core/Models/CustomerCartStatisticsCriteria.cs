using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// Criteria for a single cart/project-statistics bucket: the carts of one organization (or all the rep's served
/// organizations), optionally scoped to a store, a set of cart types and/or a set of cart statuses, whose created
/// date falls in [<see cref="FromDate"/>, <see cref="ToDate"/>] (both bounds inclusive), aggregated and converted to
/// <see cref="CurrencyCode"/>. Soft-deleted carts are always excluded. The type/status sets come from a resolved "cart
/// kind" (e.g. the built-in "active-carts" → excludes type "Wishlist"); the query layer maps business kind names to
/// these underlying filters.
/// </summary>
public class CustomerCartStatisticsCriteria : ValueObject
{
    /// <summary>
    /// Organizations (customers) whose carts are aggregated. Which organizations the caller may see is enforced
    /// upstream (in the query handler); an empty/null set aggregates nothing.
    /// </summary>
    public IList<string> OrganizationIds { get; set; }

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
    public IList<string> Types { get; set; }

    /// <summary>
    /// Optional cart-type blacklist (e.g. "Wishlist" to exclude projects). Null/empty excludes nothing. Carts with a
    /// null type are always kept (the default cart type is stored as null).
    /// </summary>
    public IList<string> ExcludeTypes { get; set; }

    /// <summary>Optional cart-status whitelist. Null/empty counts every status.</summary>
    public IList<string> Statuses { get; set; }

    /// <summary>When true, counts only non-empty carts (at least one line item). False counts carts regardless of contents.</summary>
    public bool OnlyNonEmpty { get; set; }

    /// <summary>Inclusive lower bound on the cart created date. Null = no lower bound (e.g. lifetime).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Inclusive upper bound on the cart created date. Null = no upper bound.</summary>
    public DateTime? ToDate { get; set; }
}
