using System.Collections.Generic;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Filters;

public interface IFilterRuleResolver<TRule>
    where TRule : INamedFilterRule
{
    Task<IList<TRule>> GetRulesAsync(string storeId, string cultureName);
}
