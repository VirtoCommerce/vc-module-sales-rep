using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default customer-segment source: no built-in segments. Out of the box the customers list and the counts are
/// unfiltered (the baseline served-customer set); selecting any segment name fails closed (no data) until a project
/// registers its own resolver with real segments. This keeps the surface future-proof and consistent with the order
/// and cart filter-rule domains without inventing behavioral segments the platform can't define generically.
/// </summary>
public class SalesRepCustomerFilterRuleResolver : ISalesRepCustomerFilterRuleResolver
{
    public virtual Task<IList<SalesRepCustomerFilterRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepCustomerFilterRule>>([]);

    public virtual Task<MembersSearchCriteria> ApplyListFilterAsync(string storeId, string filter, MembersSearchCriteria criteria)
        => Task.FromResult(Apply(filter, criteria));

    public virtual Task<SalesRepCustomerCountsCriteria> ApplyCountsFilterAsync(string storeId, string filter, SalesRepCustomerCountsCriteria criteria)
        => Task.FromResult(Apply(filter, criteria));

    // No segments defined: no filter → criteria unchanged (the baseline set); any named segment → null (fail-closed).
    private static TCriteria Apply<TCriteria>(string filter, TCriteria criteria) where TCriteria : class
        => string.IsNullOrEmpty(filter) ? criteria : null;
}
