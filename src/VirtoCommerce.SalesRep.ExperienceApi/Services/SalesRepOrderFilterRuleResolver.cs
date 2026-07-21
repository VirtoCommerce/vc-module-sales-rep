using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using OrderSettings = VirtoCommerce.OrdersModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default status source: each configured <c>Order.Status</c> dictionary value is a status option (1:1). Projects
/// override this service to group / hide / add statuses. Both apply methods share <see cref="ResolveStatusesAsync"/>,
/// so the list and the statistics filter identically.
/// </summary>
public class SalesRepOrderFilterRuleResolver : FilterRuleResolverBase<SalesRepOrderFilterRule>, ISalesRepOrderFilterRuleResolver
{
    private readonly ILocalizableSettingService _localizableSettingService;

    public SalesRepOrderFilterRuleResolver(ILocalizableSettingService localizableSettingService)
    {
        _localizableSettingService = localizableSettingService;
    }

    public override async Task<IList<SalesRepOrderFilterRule>> GetRulesAsync(string storeId, string cultureName)
    {
        // The platform's configured, localizable order-status dictionary (KeyValue.Key = raw status, Value = label).
        var values = await _localizableSettingService.GetValuesAsync(OrderSettings.OrderStatus.Name, cultureName);

        // Default: each configured order status is its own option (Name == the raw status; label localized).
        return values
            .Select(x => SalesRepOrderFilterRule.Create(x.Key, x.Value, x.Key))
            .ToList();
    }

    public virtual async Task<CustomerOrderSearchCriteria> ApplyListFilterAsync(string storeId, string filter, CustomerOrderSearchCriteria criteria)
    {
        var statuses = await ResolveStatusesAsync(storeId, filter);
        if (statuses == null)
        {
            return null; // fail-closed
        }

        if (statuses.Length > 0)
        {
            criteria.Statuses = statuses;
        }

        return criteria;
    }

    public virtual async Task<CustomerOrderStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, string filter, CustomerOrderStatisticsCriteria criteria)
    {
        var statuses = await ResolveStatusesAsync(storeId, filter);
        if (statuses == null)
        {
            return null; // fail-closed
        }

        if (statuses.Length > 0)
        {
            criteria.Statuses = statuses;
        }

        return criteria;
    }

    /// <summary>
    /// The single rule resolution shared by both apply methods. Tri-state: an empty array = no filter selected, or a
    /// recognized rule with no status constraint (e.g. an "All" rule) — the baseline set; a non-empty array = the
    /// recognized rule's underlying statuses; <c>null</c> = a rule name was given but is unrecognized (fail-closed).
    /// </summary>
    protected virtual async Task<string[]> ResolveStatusesAsync(string storeId, string filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return [];
        }

        var rule = await ResolveNamedRuleAsync(storeId, filter);

        return rule?.OrderStatuses;
    }
}
