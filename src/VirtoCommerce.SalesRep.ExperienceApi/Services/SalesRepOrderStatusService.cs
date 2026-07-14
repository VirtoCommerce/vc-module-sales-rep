using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using OrderSettings = VirtoCommerce.OrdersModule.Core.ModuleConstants.Settings.General;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default status source: each configured <c>Order.Status</c> dictionary value is a status option (1:1). Projects
/// override this service to group / hide / add statuses.
/// </summary>
public class SalesRepOrderStatusService : ISalesRepOrderStatusService
{
    private readonly ILocalizableSettingService _localizableSettingService;

    public SalesRepOrderStatusService(ILocalizableSettingService localizableSettingService)
    {
        _localizableSettingService = localizableSettingService;
    }

    public virtual async Task<IList<SalesRepOrderStatus>> GetStatusesAsync(string storeId, string cultureName)
    {
        // The platform's configured, localizable order-status dictionary (KeyValue.Key = raw status, Value = label).
        var values = await _localizableSettingService.GetValuesAsync(OrderSettings.OrderStatus.Name, cultureName);

        // Default: each configured order status is its own option (Name == the raw status; label localized).
        return values
            .Select(x => SalesRepOrderStatus.Create(x.Key, x.Value, x.Key))
            .ToList();
    }

    public virtual async Task<string[]> ResolveOrderStatusesAsync(string storeId, IList<string> selectedStatusNames)
    {
        if (selectedStatusNames == null || selectedStatusNames.Count == 0)
        {
            return [];
        }

        var selected = new HashSet<string>(selectedStatusNames, StringComparer.OrdinalIgnoreCase);

        // One status-list read; union the underlying statuses of every selected option.
        var statuses = await GetStatusesAsync(storeId, cultureName: null);

        return statuses
            .Where(x => selected.Contains(x.Name))
            .SelectMany(x => x.OrderStatuses)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
