using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Filters;

public abstract class FilterRuleResolverBase<TRule> : IFilterRuleResolver<TRule>
    where TRule : class, INamedFilterRule
{
    public abstract Task<IList<TRule>> GetRulesAsync(string storeId, string cultureName);

    protected async Task<TRule> ResolveNamedRuleAsync(string storeId, string filter)
    {
        var rules = await GetRulesAsync(storeId, cultureName: null);
        return rules.FirstOrDefault(x => string.Equals(x.Name, filter, StringComparison.OrdinalIgnoreCase));
    }
}
