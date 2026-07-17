using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default customer-segment source: a single "All" segment (all served customers). The customers list and the counts
/// are unfiltered when no segment — or the "All" segment — is selected (the baseline served-customer set); selecting
/// any OTHER segment name fails closed (no data) until a project registers its own resolver with real segments. This
/// gives the storefront a non-empty filter panel out of the box (one "All" chip) while keeping real segments a project
/// concern (a customer segment is a behavioral predicate the platform can't define generically). Extensible: register
/// a resolver after this (DI last-registration wins) to add segments — and, for a predicate the standard criteria
/// can't express, subclass the reader (narrow the members search's <c>ObjectIds</c>, or the counts <c>BuildQuery</c>).
/// </summary>
public class SalesRepCustomerFilterRuleResolver : ISalesRepCustomerFilterRuleResolver
{
    /// <summary>Name of the built-in "all served customers" segment — the baseline set, applies no narrowing.</summary>
    public const string AllRuleName = "All";

    public virtual Task<IList<SalesRepCustomerFilterRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepCustomerFilterRule>>([SalesRepCustomerFilterRule.Create(AllRuleName, AllRuleName)]);

    public virtual Task<MembersSearchCriteria> ApplyListFilterAsync(string storeId, string filter, MembersSearchCriteria criteria)
        => Task.FromResult(Apply(filter, criteria));

    public virtual Task<SalesRepCustomerCountsCriteria> ApplyCountsFilterAsync(string storeId, string filter, SalesRepCustomerCountsCriteria criteria)
        => Task.FromResult(Apply(filter, criteria));

    // No narrowing segments defined: no filter, or the baseline "All" segment → criteria unchanged (all served
    // customers); any other named segment → null (fail-closed).
    private static TCriteria Apply<TCriteria>(string filter, TCriteria criteria) where TCriteria : class
        => string.IsNullOrEmpty(filter) || string.Equals(filter, AllRuleName, StringComparison.OrdinalIgnoreCase)
            ? criteria
            : null;
}
