using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepCustomerFilterRuleResolver : IFilterRuleResolver<SalesRepCustomerFilterRule>
{
    Task<MembersSearchCriteria> ApplyListFilterAsync(string storeId, string filter, MembersSearchCriteria criteria);

    Task<SalesRepCustomerCountsCriteria> ApplyCountsFilterAsync(string storeId, string filter, SalesRepCustomerCountsCriteria criteria);
}
