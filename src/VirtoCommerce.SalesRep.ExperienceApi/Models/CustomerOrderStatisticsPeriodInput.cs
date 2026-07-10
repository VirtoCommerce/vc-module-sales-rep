using System;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>Input describing one date range for the <c>comparison</c> field. Omit a bound for an open-ended range.</summary>
public class CustomerOrderStatisticsPeriodInput
{
    /// <summary>Inclusive lower bound on the order created date (null = no lower bound).</summary>
    public DateTime? From { get; set; }

    /// <summary>Exclusive upper bound on the order created date (null = no upper bound).</summary>
    public DateTime? To { get; set; }
}
