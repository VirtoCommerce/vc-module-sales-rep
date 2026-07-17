using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using OrderSettings = VirtoCommerce.OrdersModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default status source: each configured <c>Order.Status</c> dictionary value is a status option (1:1). Projects
/// override this service to group / hide / add statuses. Both apply methods share <see cref="ResolveStatusesAsync"/>,
/// so the list and the statistics filter identically.
/// </summary>
public class SalesRepOrderFilterRuleResolver : ISalesRepOrderFilterRuleResolver
{
    private readonly ILocalizableSettingService _localizableSettingService;

    public SalesRepOrderFilterRuleResolver(ILocalizableSettingService localizableSettingService)
    {
        _localizableSettingService = localizableSettingService;
    }

    public virtual async Task<IList<SalesRepOrderFilterRule>> GetRulesAsync(string storeId, string cultureName)
    {
        // The platform's configured, localizable order-status dictionary (KeyValue.Key = raw status, Value = label).
        var values = await _localizableSettingService.GetValuesAsync(OrderSettings.OrderStatus.Name, cultureName);

        // Default: each configured order status is its own option (Name == the raw status; label localized).
        return values
            .Select(x => SalesRepOrderFilterRule.Create(x.Key, x.Value, x.Key))
            .ToList();
    }

    public virtual async Task<CustomerOrderSearchCriteria> ApplyListFilterAsync(string storeId, IList<string> selectedNames, CustomerOrderSearchCriteria criteria)
    {
        var statuses = await ResolveStatusesAsync(storeId, selectedNames);
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

    public virtual async Task<CustomerOrderStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, IList<string> selectedNames, CustomerOrderStatisticsCriteria criteria)
    {
        var statuses = await ResolveStatusesAsync(storeId, selectedNames);
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
    /// The single status resolution shared by both apply methods. Tri-state: an empty array = no statuses selected
    /// (no filter); a non-empty array = the deduped union of the selected options' underlying statuses; <c>null</c> =
    /// statuses were selected but none resolved (fail-closed).
    /// </summary>
    protected virtual async Task<string[]> ResolveStatusesAsync(string storeId, IList<string> selectedNames)
    {
        if (selectedNames == null || selectedNames.Count == 0)
        {
            return [];
        }

        var selected = new HashSet<string>(selectedNames, StringComparer.OrdinalIgnoreCase);

        var rules = await GetRulesAsync(storeId, cultureName: null);

        var resolved = rules
            .Where(x => selected.Contains(x.Name))
            .SelectMany(x => x.OrderStatuses)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return resolved.Length == 0 ? null : resolved;
    }
}
