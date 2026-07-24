using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.JsonConverters;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

public class LayoutService(ICustomerPreferenceService customerPreferenceService) : ILayoutService
{
    protected const string PreferenceName = "SalesRepLayout";

    // Keep JSON strings as-is (a "date:desc" sort token must not be coerced to a DateTime) and omit nulls.
    // PolymorphJsonConverter makes deserialization honor AbstractTypeFactory: a module that registers a derived
    // layout/region/block/setting type gets that type back (with its extra fields) on load. It is a no-op until an
    // override is registered (CanConvert is gated on AbstractTypeFactory<T>.HasOverrides). Writing already emits the
    // runtime type's members, so the converter is read-only.
    private static readonly JsonSerializerSettings _serializerSettings = new()
    {
        DateParseHandling = DateParseHandling.None,
        NullValueHandling = NullValueHandling.Ignore,
        Converters = { new PolymorphJsonConverter() },
    };

    public virtual async Task<Layout> GetLayoutAsync(string userId, string scope, string storeId = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(scope);

        var value = await customerPreferenceService.GetValue(userId, BuildNameParts(scope, storeId));

        return string.IsNullOrEmpty(value)
            ? null
            : JsonConvert.DeserializeObject<Layout>(value, _serializerSettings);
    }

    public virtual async Task SaveLayoutAsync(string userId, string scope, Layout layout, string storeId = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(scope);
        ArgumentNullException.ThrowIfNull(layout);

        layout.ModifiedDate = DateTime.UtcNow;

        var value = JsonConvert.SerializeObject(layout, _serializerSettings);

        await customerPreferenceService.SaveValue(userId, BuildNameParts(scope, storeId), value);
    }

    // Per-user key, optionally scoped to a store: "SalesRepLayout.{scope}[.{storeId}]".
    protected virtual IList<string> BuildNameParts(string scope, string storeId)
    {
        List<string> parts = [PreferenceName, scope];

        if (!string.IsNullOrEmpty(storeId))
        {
            parts.Add(storeId);
        }

        return parts;
    }
}
