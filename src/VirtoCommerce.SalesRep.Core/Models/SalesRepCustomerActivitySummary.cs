using System;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepCustomerActivitySummary
{
    public DateTime? CreatedOn { get; set; }

    public DateTime? LastWebLogin { get; set; }

    public int VisitsCount { get; set; }

    public string LastSearchTerm { get; set; }

    public DateTime? LastSearchedDate { get; set; }

    public SalesRepActivityProduct LastViewedProduct { get; set; }

    public DateTime? LastViewedDate { get; set; }

    // Defaults FALSE, unlike the feed's result: every early return of its one builder measured nothing.
    public bool IsAnalyticsAvailable { get; set; }
}
