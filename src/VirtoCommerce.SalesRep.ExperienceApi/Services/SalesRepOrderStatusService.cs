using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using OrderSettings = VirtoCommerce.OrdersModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepOrderStatusService : ISalesRepOrderStatusService
{
    private readonly ILocalizableSettingService _localizableSettingService;

    public SalesRepOrderStatusService(ILocalizableSettingService localizableSettingService)
    {
        _localizableSettingService = localizableSettingService;
    }

    public virtual async Task<IList<SalesRepOrderStatus>> GetStatusesAsync(string storeId, string cultureName)
    {
        var values = await _localizableSettingService.GetValuesAsync(OrderSettings.OrderStatus.Name, cultureName);

        return values
            .Select(x => SalesRepOrderStatus.Create(x.Key, x.Value, x.Key))
            .ToList();
    }

    public virtual async Task<IList<string>> ResolveOrderStatusesAsync(string storeId, IList<string> selectedStatusNames)
    {
        if (selectedStatusNames == null || selectedStatusNames.Count == 0)
        {
            return [];
        }

        var selected = new HashSet<string>(selectedStatusNames, StringComparer.OrdinalIgnoreCase);

        var statuses = await GetStatusesAsync(storeId, cultureName: null);

        return statuses
            .Where(x => selected.Contains(x.Name))
            .SelectMany(x => x.OrderStatuses)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
