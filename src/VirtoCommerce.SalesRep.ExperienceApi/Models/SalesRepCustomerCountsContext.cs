using System;
using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Backing object for the "my customers" counts field: the shared organization / rep / store scope (from
/// <see cref="SalesRepStatisticsContext"/>) plus the per-organization assignment dates. Date ranges come from the
/// sub-field arguments. No currency axis — the counters are cardinalities, not money.
/// </summary>
public class SalesRepCustomerCountsContext : SalesRepStatisticsContext
{
    /// <summary>
    /// The date each served organization was first assigned to the rep (the earliest granting-membership creation
    /// date per organization) — resolved once by the handler. The range-dependent "new customers" counter counts how
    /// many of these fall in a given window, so a customer assigned recently counts as new regardless of when the
    /// organization itself was created or first ordered.
    /// </summary>
    public IList<DateTime> AssignmentDates { get; set; }
}
