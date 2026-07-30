using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.ExperienceApi.Filters;

/// <summary>
/// What a filter-rule resolver knows about the caller when it builds or resolves its rules: the store and culture for
/// labels, plus the data scope the rules will be applied within — the rep's served organizations (narrowed to one when
/// a single customer is being viewed), the rep themself as the creator of the records, and the selected period. A
/// data-derived rule set (e.g. the order statuses actually in use, or the categories actually sold into) must be built
/// within that same scope, or it offers rules the caller's list returns nothing for.
/// </summary>
public class SalesRepFilterRuleContext
{
    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public IList<string> OrganizationIds { get; set; } = [];

    public string CustomerId { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public static SalesRepFilterRuleContext Create(
        string storeId,
        string cultureName,
        IList<string> organizationIds,
        string customerId,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var result = AbstractTypeFactory<SalesRepFilterRuleContext>.TryCreateInstance();
        result.StoreId = storeId;
        result.CultureName = cultureName;
        result.OrganizationIds = organizationIds ?? [];
        result.CustomerId = customerId;
        result.FromDate = fromDate;
        result.ToDate = toDate;
        return result;
    }
}
