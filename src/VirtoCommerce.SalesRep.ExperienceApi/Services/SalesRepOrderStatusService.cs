using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using OrdersModuleConstants = VirtoCommerce.OrdersModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

/// <summary>
/// Default status source: each configured <c>Order.Status</c> dictionary value is a status option (1:1), and an
/// order's raw status is localized straight from that dictionary. Projects override this service to group / hide /
/// add statuses or to change how statuses are localized.
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
        var values = await GetOrderStatusValuesAsync(cultureName);

        // Default: each configured order status is its own option (Name == the raw status; label localized).
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

    public virtual async Task<IDictionary<string, string>> GetLocalizedStatusesAsync(string storeId, string cultureName)
    {
        // Raw order status → localized label, straight from the order-status dictionary.
        var values = await GetOrderStatusValuesAsync(cultureName);

        return values
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Value, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The order-status dictionary values (KeyValue.Key = raw status, Value = localized label).</summary>
    protected virtual Task<IList<KeyValue>> GetOrderStatusValuesAsync(string cultureName)
        => _localizableSettingService.GetValuesAsync(OrdersModuleConstants.Settings.General.OrderStatus.Name, cultureName);
}
