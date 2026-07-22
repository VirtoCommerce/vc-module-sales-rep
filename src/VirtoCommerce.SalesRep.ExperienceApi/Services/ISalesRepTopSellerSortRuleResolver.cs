using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepTopSellerSortRuleResolver : ISortRuleResolver<SalesRepTopSellerSortRule>
{
    Task<SalesRepTopSellerCriteria> ApplySortAsync(string storeId, string sort, SalesRepTopSellerCriteria criteria);
}
