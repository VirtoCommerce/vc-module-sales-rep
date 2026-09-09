using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;
using VirtoCommerce.TaskManagement.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public interface ISalesRepTaskSortRuleResolver : ISortRuleResolver<SalesRepTaskSortRule>
{
    Task<WorkTaskSearchCriteria> ApplySortAsync(string storeId, string sort, WorkTaskSearchCriteria criteria);
}
