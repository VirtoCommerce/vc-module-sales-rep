namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// "My customers" counters for a single date range. The number of assigned customers is period-independent and
/// resolved at the query layer from the served-organization set; this holds the range-dependent counters:
/// "ordering customers" from the rep's own orders, "new customers" from customer assignment dates.
/// </summary>
public class SalesRepCustomerCountsPeriod
{
    /// <summary>Distinct organizations the rep placed at least one order for within the range ("ordered this month").</summary>
    public int OrderingCustomers { get; set; }

    /// <summary>Organizations first assigned to the rep within the range ("new customers"), by assignment date.</summary>
    public int NewCustomers { get; set; }
}
