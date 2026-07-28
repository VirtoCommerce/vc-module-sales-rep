using System;
using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCustomerCountsContext : SalesRepStatisticsContext
{
    public IList<DateTime> AssignmentDates { get; set; }
}
