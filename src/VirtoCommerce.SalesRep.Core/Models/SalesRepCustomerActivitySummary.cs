using System;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepCustomerActivitySummary
{
    public DateTime? CreatedOn { get; set; }

    public DateTime? LastWebLogin { get; set; }

    public int VisitsCount { get; set; }

    public string LastSearchTerm { get; set; }

    public SalesRepActivityProduct LastViewedProduct { get; set; }

    // Defaults FALSE, unlike the feed's result: this is built in one place, so every early return is a path
    // that measured nothing.
    public bool IsAnalyticsAvailable { get; set; }
}
