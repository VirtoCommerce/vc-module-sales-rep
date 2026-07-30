using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Filters;

public abstract class FilterRuleResolverBase<TRule> : IFilterRuleResolver<TRule>
    where TRule : class, INamedFilterRule
{
    public abstract Task<IList<TRule>> GetRulesAsync(SalesRepFilterRuleContext context);

    /// <summary>
    /// Resolves a selected rule name within the scope the rules are about to be applied in — the same scope the
    /// discovery query built them in, so what the storefront was offered is exactly what resolves.
    /// </summary>
    protected async Task<TRule> ResolveNamedRuleAsync(SalesRepFilterRuleContext context, string filter)
    {
        var rules = await GetRulesAsync(context);
        return rules.FirstOrDefault(x => string.Equals(x.Name, filter, StringComparison.OrdinalIgnoreCase));
    }
}
