using System;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Input describing one date range for a statistics <c>comparison</c> field (orders, carts, customers). Omit a
/// bound for an open-ended range. Shared by every Sales Rep statistics query so their comparison inputs stay identical.
/// </summary>
public class SalesRepStatisticsPeriodInput
{
    /// <summary>Inclusive lower bound on the created date (null = no lower bound).</summary>
    public DateTime? From { get; set; }

    /// <summary>Exclusive upper bound on the created date (null = no upper bound).</summary>
    public DateTime? To { get; set; }
}
