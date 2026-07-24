using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Models.Dashboard;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

public class DashboardLayoutService(ICustomerPreferenceService customerPreferenceService) : IDashboardLayoutService
{
    protected const string PreferenceName = "SalesRepDashboardLayout";

    // Keep JSON strings as-is (a "date:desc" sort token must not be coerced to a DateTime) and omit nulls.
    private static readonly JsonSerializerSettings _serializerSettings = new()
    {
        DateParseHandling = DateParseHandling.None,
        NullValueHandling = NullValueHandling.Ignore,
    };

    public virtual async Task<DashboardLayout> GetLayoutAsync(string userId, string scope, string storeId = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(scope);

        var value = await customerPreferenceService.GetValue(userId, BuildNameParts(scope, storeId));

        return string.IsNullOrEmpty(value)
            ? null
            : JsonConvert.DeserializeObject<DashboardLayout>(value, _serializerSettings);
    }

    public virtual async Task SaveLayoutAsync(string userId, string scope, DashboardLayout layout, string storeId = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(scope);
        ArgumentNullException.ThrowIfNull(layout);

        layout.ModifiedDate = DateTime.UtcNow;

        var value = JsonConvert.SerializeObject(layout, _serializerSettings);

        await customerPreferenceService.SaveValue(userId, BuildNameParts(scope, storeId), value);
    }

    // Per-user key, optionally scoped to a store: "SalesRepDashboardLayout.{scope}[.{storeId}]".
    protected virtual IList<string> BuildNameParts(string scope, string storeId)
    {
        var parts = new List<string> { PreferenceName, scope };

        if (!string.IsNullOrEmpty(storeId))
        {
            parts.Add(storeId);
        }

        return parts;
    }
}
