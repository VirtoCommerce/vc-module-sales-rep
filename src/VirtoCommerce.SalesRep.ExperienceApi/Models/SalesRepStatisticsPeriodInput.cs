using System;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Input describing one date range for a statistics <c>comparison</c> field (orders, carts, customers). Omit a
/// bound for an open-ended range. Shared by every Sales Rep statistics query so their comparison inputs stay identical.
/// <para>
/// Both bounds are <b>inclusive</b> and compared as UTC instants against the record's UTC-stored created date (the
/// column is <c>timestamp with time zone</c>). The caller sends the time component and owns any local-to-UTC
/// conversion — exactly as the storefront orders date filter does, e.g. a user's local day
/// <c>[00:00:00.000, 23:59:59.999]</c> is sent as its UTC equivalent (<c>…T05:00:00.000Z</c> … <c>…T04:59:59.999Z</c>
/// for a UTC-5 user). There is no server-side date truncation.
/// </para>
/// </summary>
public class SalesRepStatisticsPeriodInput
{
    /// <summary>Inclusive lower bound on the created date (null = no lower bound).</summary>
    public DateTime? From { get; set; }

    /// <summary>Inclusive upper bound on the created date (null = no upper bound).</summary>
    public DateTime? To { get; set; }
}
