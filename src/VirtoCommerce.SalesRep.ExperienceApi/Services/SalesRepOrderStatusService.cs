using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using OrdersModuleConstants = VirtoCommerce.OrdersModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default status source: each configured <c>Order.Status</c> dictionary value becomes its own tab (1:1), with the
/// localized label from the settings dictionary. Projects override this service to group / hide / add statuses.
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
        var values = await _localizableSettingService.GetValuesAsync(OrdersModuleConstants.Settings.General.OrderStatus.Name, cultureName);

        // Default: each configured order status is its own tab (Name == the raw status; label localized).
        return values
            .Select(x => SalesRepOrderStatus.Create(x.Key, x.Value, x.Key))
            .ToList();
    }

    public virtual async Task<string[]> ResolveOrderStatusesAsync(string storeId, string selectedStatusName)
    {
        if (string.IsNullOrEmpty(selectedStatusName))
        {
            return [];
        }

        var statuses = await GetStatusesAsync(storeId, cultureName: null);
        var selected = statuses.FirstOrDefault(x => x.Name.EqualsIgnoreCase(selectedStatusName));

        return selected?.OrderStatuses ?? [];
    }
}
