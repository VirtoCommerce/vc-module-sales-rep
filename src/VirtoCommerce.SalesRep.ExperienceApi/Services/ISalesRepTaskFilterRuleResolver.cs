using System;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.TaskManagement.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepTaskFilterRuleResolver : IFilterRuleResolver<SalesRepTaskFilterRule>
{
    Task<WorkTaskSearchCriteria> ApplyListFilterAsync(string storeId, string filter, WorkTaskSearchCriteria criteria, DateTime dayStart);
}
