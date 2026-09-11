using System;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepCustomerActivitySummary
{
    public DateTime? CreatedOn { get; set; }

    public DateTime? LastWebLogin { get; set; }

    public int VisitsCount { get; set; }

    public string LastSearchTerm { get; set; }

    public SalesRepActivityProduct LastViewedProduct { get; set; }

    public bool IsAnalyticsConfigured { get; set; }
}
