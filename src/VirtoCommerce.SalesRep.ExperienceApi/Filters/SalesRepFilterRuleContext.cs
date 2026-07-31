using System;
using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Filters;

/// <summary>
/// What a filter-rule resolver knows about the caller: store and culture for the labels, plus the scope the rules will
/// be applied within (organizations served — one when a customer page is being viewed, the rep as record creator, the
/// selected period). A data-derived rule set has to be built in that same scope, or it offers rules the caller's list
/// returns nothing for.
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

    /// <summary>The scope alone, for the lookups that derive a vocabulary from the data.</summary>
    public virtual SalesRepScopeCriteria ToScopeCriteria()
        => SalesRepScopeCriteria.Create(OrganizationIds, CustomerId, StoreId, FromDate, ToDate);
}
