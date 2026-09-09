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

        // The reader's own criteria carry the scope the rules must be resolved in, as in the order and cart
        // resolvers. No customer id: task ownership is the plural ResponsibleIds, which the context has no slot for.
        var context = SalesRepFilterRuleContext.Create(
            storeId, cultureName: null, criteria.OrganizationIds, customerId: null, criteria.StartDueDate, criteria.EndDueDate);

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
                // A millisecond, not a tick: EndDueDate compares inclusively and 100 ns is not representable on
                // PostgreSQL or MySQL DATETIME(6). The cost is the final millisecond before midnight, which nothing
                // the storefront writes can land in - it emits day-aligned due dates.
                criteria.EndDueDate = Earliest(criteria.EndDueDate, dayStart.AddMilliseconds(-1));
                break;
            case CompletedRuleName:
                criteria.IsActive = false;
                criteria.Completed = true;
                break;

            // Fail closed, like the unknown-filter path above: a rule that GetRulesAsync offers but an override
            // forgot to implement here must not fall through to the unfiltered list.
            default:
                return null;
        }

        return criteria;
    }

    private static DateTime Latest(DateTime? current, DateTime candidate) =>
        current == null || candidate > current.Value ? candidate : current.Value;

    private static DateTime Earliest(DateTime? current, DateTime candidate) =>
        current == null || candidate < current.Value ? candidate : current.Value;
}
