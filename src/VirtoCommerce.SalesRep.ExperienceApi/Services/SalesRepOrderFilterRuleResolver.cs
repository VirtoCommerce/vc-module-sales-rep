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

public class SalesRepOrderFilterRuleResolver : FilterRuleResolverBase<SalesRepOrderFilterRule>, ISalesRepOrderFilterRuleResolver
{
    private readonly ILocalizableSettingService _localizableSettingService;

    public SalesRepOrderFilterRuleResolver(ILocalizableSettingService localizableSettingService)
    {
        _localizableSettingService = localizableSettingService;
    }

    public override async Task<IList<SalesRepOrderFilterRule>> GetRulesAsync(string storeId, string cultureName)
    {
        var values = await _localizableSettingService.GetValuesAsync(OrderSettings.OrderStatus.Name, cultureName);

        return values
            .Select(x => SalesRepOrderFilterRule.Create(x.Key, x.Value, x.Key))
            .ToList();
    }

    public virtual async Task<CustomerOrderSearchCriteria> ApplyListFilterAsync(string storeId, string filter, CustomerOrderSearchCriteria criteria)
    {
        var statuses = await ResolveStatusesAsync(storeId, filter);
        if (statuses == null)
        {
            return null;
        }

        if (statuses.Count > 0)
        {
            criteria.Statuses = statuses.ToArray();
        }

        return criteria;
    }

    public virtual async Task<CustomerOrderStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, string filter, CustomerOrderStatisticsCriteria criteria)
    {
        var statuses = await ResolveStatusesAsync(storeId, filter);
        if (statuses == null)
        {
            return null;
        }

        if (statuses.Count > 0)
        {
            criteria.Statuses = statuses;
        }

        return criteria;
    }

    protected virtual async Task<IList<string>> ResolveStatusesAsync(string storeId, string filter)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return [];
        }

        var rule = await ResolveNamedRuleAsync(storeId, filter);

        return rule?.OrderStatuses;
    }
}
