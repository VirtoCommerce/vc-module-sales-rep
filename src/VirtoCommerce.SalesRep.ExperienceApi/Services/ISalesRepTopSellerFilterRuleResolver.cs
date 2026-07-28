using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepTopSellerFilterRuleResolver : IFilterRuleResolver<SalesRepTopSellerFilterRule>
{
    Task<SalesRepTopSellerCriteria> ApplyListFilterAsync(string storeId, string filter, SalesRepTopSellerCriteria criteria);
}
