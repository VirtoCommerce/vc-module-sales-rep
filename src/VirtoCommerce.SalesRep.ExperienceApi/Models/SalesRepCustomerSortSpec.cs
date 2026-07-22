using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

public class SalesRepCustomerSortSpec
{
    public bool IsOrderDerived { get; set; }

    public string MemberSortField { get; set; }

    public SalesRepCustomerSortMetric Metric { get; set; }

    public SortDirection Direction { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}
