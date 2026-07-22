using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepCartFilterRuleResolver : IFilterRuleResolver<SalesRepCartFilterRule>
{
    Task<CustomerCartStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, string filter, CustomerCartStatisticsCriteria criteria);
}
