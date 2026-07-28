using System.Collections.Generic;
using System.Threading.Tasks;

namespace VirtoCommerce.SalesRep.ExperienceApi.Sorts;

public interface ISortRuleResolver<TRule>
    where TRule : INamedSortRule
{
    Task<IList<TRule>> GetRulesAsync(string storeId, string cultureName);
}
