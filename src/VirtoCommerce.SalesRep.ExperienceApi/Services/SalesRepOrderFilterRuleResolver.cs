using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using OrderSettings = VirtoCommerce.OrdersModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepOrderFilterRuleResolver : FilterRuleResolverBase<SalesRepOrderFilterRule>, ISalesRepOrderFilterRuleResolver
{
    private readonly ILocalizableSettingService _localizableSettingService;
    private readonly ISalesRepOrderStatusService _orderStatusService;

    public SalesRepOrderFilterRuleResolver(
        ILocalizableSettingService localizableSettingService,
        ISalesRepOrderStatusService orderStatusService)
    {
        _localizableSettingService = localizableSettingService;
        _orderStatusService = orderStatusService;
    }

    /// <summary>
    /// One 1:1 rule per status the orders in the caller's scope actually use — not per configured status: a status that
    /// arrives with an order from outside the platform (e.g. an ERP sync) is offered as a filter, while a status none of
    /// the rep's (or the viewed customer's) orders carry is not offered at all. The configured <c>Order.Status</c>
    /// dictionary still supplies the curated order and the localized labels; statuses missing from it are appended
    /// alphabetically, labeled with the raw status.
    /// </summary>
    public override async Task<IList<SalesRepOrderFilterRule>> GetRulesAsync(SalesRepFilterRuleContext context)
    {
        var usedStatuses = await _orderStatusService.GetUsedStatusesAsync(context.ToScopeCriteria());
        if (usedStatuses.Count == 0)
        {
            return [];
        }

        var used = usedStatuses.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var configured = await _localizableSettingService.GetValuesAsync(OrderSettings.OrderStatus.Name, context.CultureName);

        var configuredRules = configured
            .Where(x => used.Contains(x.Key))
            .Select(x => SalesRepOrderFilterRule.Create(x.Key, x.Value, x.Key));

        var unconfiguredRules = usedStatuses
            .Except(configured.Select(x => x.Key), StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => SalesRepOrderFilterRule.Create(x, x, x));

        return configuredRules.Concat(unconfiguredRules).ToList();
    }

    public virtual async Task<CustomerOrderSearchCriteria> ApplyListFilterAsync(string storeId, string filter, CustomerOrderSearchCriteria criteria)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return criteria;
        }

        // The reader's own criteria carry the scope the rules must be resolved in (the same organizations, creator and
        // date window the list is about to be searched with).
        var context = SalesRepFilterRuleContext.Create(
            storeId, cultureName: null, criteria.OrganizationIds, criteria.CustomerId, criteria.StartDate, criteria.EndDate);

        var statuses = await ResolveStatusesAsync(context, filter);
        if (statuses.IsNullOrEmpty())
        {
            return null;
        }

        criteria.Statuses = statuses.ToArray();

        return criteria;
    }

    public virtual async Task<CustomerOrderStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, string filter, CustomerOrderStatisticsCriteria criteria)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return criteria;
        }

        var context = SalesRepFilterRuleContext.Create(
            storeId, cultureName: null, criteria.OrganizationIds, criteria.CustomerId, criteria.FromDate, criteria.ToDate);

        var statuses = await ResolveStatusesAsync(context, filter);
        if (statuses.IsNullOrEmpty())
        {
            return null;
        }

        criteria.Statuses = statuses;

        return criteria;
    }

    /// <summary>
    /// The statuses a selected rule stands for, or null when the name is not one of the rules offered in this scope.
    /// Callers treat "resolved to no statuses" the same as "unknown rule": a filter that matches nothing must never
    /// fall through to an unfiltered read.
    /// </summary>
    protected virtual async Task<IList<string>> ResolveStatusesAsync(SalesRepFilterRuleContext context, string filter)
    {
        var rule = await ResolveNamedRuleAsync(context, filter);

        return rule?.OrderStatuses;
    }
}
