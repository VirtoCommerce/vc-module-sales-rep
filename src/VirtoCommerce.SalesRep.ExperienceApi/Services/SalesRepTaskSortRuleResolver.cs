using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;
using VirtoCommerce.TaskManagement.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepTaskSortRuleResolver : SortRuleResolverBase<SalesRepTaskSortRule>, ISalesRepTaskSortRuleResolver
{
    public const string DueDateRuleName = "due-date";

    public const string RecentRuleName = "recent";

    public const string NameRuleName = "name";

    // IMPORTANT (keep): no priority rule. WorkTask.Priority is persisted as the enum's NAME, so the database sorts it
    // alphabetically (High, Highest, Low, Lowest, Normal) - never by rank. Offering it would look like a bug.
    public override Task<IList<SalesRepTaskSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepTaskSortRule>>(
        [
            SalesRepTaskSortRule.Create(DueDateRuleName, "Due date", "dueDate", SortDirection.Ascending, supportsDirection: true),
            SalesRepTaskSortRule.Create(RecentRuleName, "Recently created", "createdDate", SortDirection.Descending, supportsDirection: false),
            SalesRepTaskSortRule.Create(NameRuleName, "Task", "name", SortDirection.Ascending, supportsDirection: true),
        ]);

    public virtual async Task<WorkTaskSearchCriteria> ApplySortAsync(string storeId, string sort, WorkTaskSearchCriteria criteria)
    {
        await ApplyResolvedSortAsync(storeId, sort, criteria);

        return criteria;
    }
}
