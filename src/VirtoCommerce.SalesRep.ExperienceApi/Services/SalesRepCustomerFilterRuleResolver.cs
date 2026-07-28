using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepCustomerFilterRuleResolver : ISalesRepCustomerFilterRuleResolver
{
    public const string AllRuleName = "All";

    public virtual Task<IList<SalesRepCustomerFilterRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepCustomerFilterRule>>([SalesRepCustomerFilterRule.Create(AllRuleName, AllRuleName)]);

    public virtual Task<MembersSearchCriteria> ApplyListFilterAsync(string storeId, string filter, MembersSearchCriteria criteria)
        => Task.FromResult(Apply(filter, criteria));

    public virtual Task<SalesRepCustomerCountsCriteria> ApplyCountsFilterAsync(string storeId, string filter, SalesRepCustomerCountsCriteria criteria)
        => Task.FromResult(Apply(filter, criteria));

    private static TCriteria Apply<TCriteria>(string filter, TCriteria criteria) where TCriteria : class
        => string.IsNullOrEmpty(filter) || string.Equals(filter, AllRuleName, StringComparison.OrdinalIgnoreCase)
            ? criteria
            : null;
}
