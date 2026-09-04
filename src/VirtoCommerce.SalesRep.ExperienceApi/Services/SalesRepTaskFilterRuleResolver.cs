using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.TaskManagement.Core.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepTaskFilterRuleResolver : FilterRuleResolverBase<SalesRepTaskFilterRule>, ISalesRepTaskFilterRuleResolver
{
    public const string UpcomingRuleName = "upcoming";

    public const string OverdueRuleName = "overdue";

    public const string CompletedRuleName = "completed";

    public override Task<IList<SalesRepTaskFilterRule>> GetRulesAsync(SalesRepFilterRuleContext context)
        => Task.FromResult<IList<SalesRepTaskFilterRule>>(
        [
            SalesRepTaskFilterRule.Create(UpcomingRuleName, "Upcoming"),
            SalesRepTaskFilterRule.Create(OverdueRuleName, "Overdue"),
            SalesRepTaskFilterRule.Create(CompletedRuleName, "Completed"),
        ]);

    public virtual async Task<WorkTaskSearchCriteria> ApplyListFilterAsync(string storeId, string filter, WorkTaskSearchCriteria criteria, DateTime dayStart)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return criteria;
        }

        var context = SalesRepFilterRuleContext.Create(storeId, cultureName: null, organizationIds: null, customerId: null);

        var rule = await ResolveNamedRuleAsync(context, filter);
        if (rule == null)
        {
            return null;
        }

        return Apply(criteria, rule.Name, dayStart);
    }

    protected virtual WorkTaskSearchCriteria Apply(WorkTaskSearchCriteria criteria, string ruleName, DateTime dayStart)
    {
        switch (ruleName)
        {
            case UpcomingRuleName:
                criteria.IsActive = true;
                // Due today or later, on the caller's calendar. Narrowed, not assigned: the calendar views also pass a
                // due-date window, and a tab must intersect with it rather than replace it.
                criteria.StartDueDate = Latest(criteria.StartDueDate, dayStart);
                break;
            case OverdueRuleName:
                criteria.IsActive = true;
                // Strictly before the start of the caller's today, so a task due at exactly 00:00 reads as upcoming.
                criteria.EndDueDate = Earliest(criteria.EndDueDate, dayStart.AddTicks(-1));
                break;
            case CompletedRuleName:
                criteria.IsActive = false;
                criteria.Completed = true;
                break;
        }

        return criteria;
    }

    private static DateTime Latest(DateTime? current, DateTime candidate) =>
        current == null || candidate > current.Value ? candidate : current.Value;

    private static DateTime Earliest(DateTime? current, DateTime candidate) =>
        current == null || candidate < current.Value ? candidate : current.Value;
}
