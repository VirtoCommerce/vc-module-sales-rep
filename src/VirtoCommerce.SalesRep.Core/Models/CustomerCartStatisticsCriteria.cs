using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class CustomerCartStatisticsCriteria : ValueObject
{
    public IList<string> OrganizationIds { get; set; }

    public string CustomerId { get; set; }

    public string StoreId { get; set; }

    public string CurrencyCode { get; set; }

    public IList<string> Names { get; set; }

    public IList<string> Types { get; set; }

    public IList<string> ExcludeTypes { get; set; }

    public IList<string> Statuses { get; set; }

    public bool IncludeCartFigures { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}
