using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepOrderFilterRuleResolver : IFilterRuleResolver<SalesRepOrderFilterRule>
{
    Task<CustomerOrderSearchCriteria> ApplyListFilterAsync(string storeId, string filter, CustomerOrderSearchCriteria criteria);

    Task<CustomerOrderStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, string filter, CustomerOrderStatisticsCriteria criteria);
}
