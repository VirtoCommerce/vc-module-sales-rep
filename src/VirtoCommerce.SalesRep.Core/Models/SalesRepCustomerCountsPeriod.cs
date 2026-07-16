namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// "My customers" counters for a single date range, derived from the rep's own orders. The number of assigned
/// customers is period-independent and resolved at the query layer from the served-organization set; this holds the
/// range-dependent counters.
/// </summary>
public class SalesRepCustomerCountsPeriod
{
    /// <summary>Distinct organizations the rep placed at least one order for within the range ("ordered this month").</summary>
    public int OrderingCustomers { get; set; }

    /// <summary>Organizations whose first-ever order by the rep falls in the range ("new customers").</summary>
    public int NewCustomers { get; set; }
}
