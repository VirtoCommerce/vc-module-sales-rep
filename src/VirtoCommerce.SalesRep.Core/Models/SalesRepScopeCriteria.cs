using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

/// <summary>
/// The records a rep may read: organizations served, own created records, one store, one period. Carries no ranking
/// input (take, sort, currency) — those would split a cached vocabulary across keys holding the same answer.
/// </summary>
public class SalesRepScopeCriteria : ValueObject
{
    public IList<string> OrganizationIds { get; set; }

    public string CustomerId { get; set; }

    public string StoreId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public static SalesRepScopeCriteria Create(IList<string> organizationIds, string customerId, string storeId, DateTime? fromDate, DateTime? toDate)
    {
        var result = AbstractTypeFactory<SalesRepScopeCriteria>.TryCreateInstance();
        result.OrganizationIds = organizationIds;
        result.CustomerId = customerId;
        result.StoreId = storeId;
        result.FromDate = fromDate;
        result.ToDate = toDate;
        return result;
    }
}
