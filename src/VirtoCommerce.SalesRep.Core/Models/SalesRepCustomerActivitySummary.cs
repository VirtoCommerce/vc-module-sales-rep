using System;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepCustomerActivitySummary
{
    public DateTime? CreatedOn { get; set; }

    public DateTime? LastWebLogin { get; set; }

    public int VisitsCount { get; set; }

    public string LastSearchTerm { get; set; }

    public SalesRepActivityProduct LastViewedProduct { get; set; }

    // One generic signal: false for an absent or unconfigured analytics module and for a read that failed
    // alike, meaning the figures beside it are not measurements. Defaults FALSE — unlike the feed's result,
    // this is built in one place, so every early return is a path that measured nothing.
    public bool IsAnalyticsAvailable { get; set; }
}
