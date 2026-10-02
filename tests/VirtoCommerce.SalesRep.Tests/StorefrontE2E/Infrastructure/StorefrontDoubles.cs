using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.PaymentModule.Core.Model;
using VirtoCommerce.ShippingModule.Core.Model;
using VirtoCommerce.XCart.Core.Models;
using CartAggregate = VirtoCommerce.XCart.Core.CartAggregate;
using VirtoCommerce.XCart.Core.Services;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;
using XapiModuleConstants = VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>No module-federation plugins: the store type's app manifest field resolves to nothing.</summary>
internal sealed class NullAppManifestService : IAppManifestService
{
    public AppManifestDescriptor GetManifest(string appId) => null;
}

/// <summary>The cart type's constructor dependency; the storefront's short cart never resolves these fields here.</summary>
internal sealed class NoCartAvailMethodsService : ICartAvailMethodsService
{
    public Task<IEnumerable<PaymentMethod>> GetAvailablePaymentMethodsAsync(CartAggregate cartAggregate) => Task.FromResult<IEnumerable<PaymentMethod>>([]);
    public Task<IEnumerable<ShippingRate>> GetAvailableShippingRatesAsync(CartAggregate cartAggregate) => Task.FromResult<IEnumerable<ShippingRate>>([]);
    public Task<IEnumerable<GiftItem>> GetAvailableGiftsAsync(CartAggregate cartAggregate) => Task.FromResult<IEnumerable<GiftItem>>([]);
}

/// <summary>No store is resolved by domain; the resolver then falls back to StoresOptions.DefaultStore.</summary>
internal sealed class EmptyStoreSearchService : IStoreSearchService
{
    public Task<StoreSearchResult> SearchAsync(StoreSearchCriteria criteria, bool clone = true)
        => Task.FromResult(AbstractTypeFactory<StoreSearchResult>.TryCreateInstance());
}

/// <summary>Every store offers the harness's fixed currency set (USD primary, EUR).</summary>
internal sealed class CurrencyServiceStoreCurrencyResolver(ICurrencyService currencyService) : IStoreCurrencyResolver
{
    public async Task<Currency> GetStoreCurrencyAsync(string currencyCode, string storeId, string cultureName = null)
        => (await currencyService.GetAllCurrenciesAsync()).FirstOrDefault(x => x.Code.EqualsIgnoreCase(currencyCode));

    public async Task<IEnumerable<Currency>> GetAllStoreCurrenciesAsync(string storeId, string cultureName = null)
        => await currencyService.GetAllCurrenciesAsync();
}

/// <summary>The storefront's sign-in form needs the password scheme listed as active.</summary>
internal sealed class PasswordOnlyStoreAuthenticationService : IStoreAuthenticationService
{
    public Task<IList<StoreAuthenticationScheme>> GetStoreSchemesAsync(string storeId, bool clone = true)
        => Task.FromResult<IList<StoreAuthenticationScheme>>([new StoreAuthenticationScheme { Name = "Password", IsActive = true }]);

    public Task SaveStoreSchemesAsync(string storeId, IList<StoreAuthenticationScheme> models) => Task.CompletedTask;
}

/// <summary>
/// The module list the store query reports to the storefront. Task management is listed so the storefront's calendar
/// gate (a presence check on the module id, the way the real module list reaches it with ReturnModuleVersion on)
/// opens; white labeling is absent so the storefront drops its @gate'd field client-side.
/// </summary>
internal sealed class StorefrontModuleService : IModuleService
{
    private static readonly IList<ManifestModuleInfo> _installed =
    [
        Module("VirtoCommerce.SalesRep", "3.1012.0"),
        Module("VirtoCommerce.TaskManagement", "3.1005.0"),
    ];

    public IList<ManifestModuleInfo> GetModules() => _installed;
    public IList<ManifestModuleInfo> GetInstalledModules() => _installed;
    public IList<ManifestModuleInfo> GetFailedModules() => [];
    public bool IsInstalled(string moduleId) => GetModule(moduleId) != null;
    public bool IsInstalled(string moduleId, string minVersion) => IsInstalled(moduleId);
    public ManifestModuleInfo GetModule(string moduleId) => _installed.FirstOrDefault(x => x.Id.EqualsIgnoreCase(moduleId));

    private static ManifestModuleInfo Module(string id, string version)
        => new ManifestModuleInfo().LoadFromManifest(new ModuleManifest { Id = id, Version = version, PlatformVersion = "3.1048.0" });
}

/// <summary>
/// The harness's minimal settings double plus the one storefront-relevant value: ReturnModuleVersion on, so the store
/// query appends the installed modules without public settings (task management) to store.settings.modules.
/// </summary>
internal sealed class StorefrontSettingsManager : ISettingsManager
{
    public IEnumerable<SettingDescriptor> AllRegisteredSettings => [];
    public void RegisterSettings(IEnumerable<SettingDescriptor> settings, string moduleId = null) { }
    public void RegisterSettingsForType(IEnumerable<SettingDescriptor> settings, string typeName) { }
    public IEnumerable<SettingDescriptor> GetSettingsForType(string typeName) => [];
    public IDictionary<string, string[]> GetSettingTypeAssignments() => new Dictionary<string, string[]>();

    public Task<ObjectSettingEntry> GetObjectSettingAsync(string name, string objectType = null, string objectId = null)
    {
        var setting = new ObjectSettingEntry
        {
            Name = name,
            // Non-null AllowedValues everywhere: FileExtensionService unions the white/blacklist settings' AllowedValues.
            AllowedValues = name == PlatformConstants.Settings.General.Languages.Name ? ["en-US", "de-DE"] : [],
        };

        if (name == XapiModuleConstants.Settings.General.ReturnModuleVersion.Name)
        {
            setting.ValueType = SettingValueType.Boolean;
            setting.Value = true;
        }

        return Task.FromResult(setting);
    }

    public Task<IEnumerable<ObjectSettingEntry>> GetObjectSettingsAsync(IEnumerable<string> names, string objectType = null, string objectId = null)
        => Task.FromResult<IEnumerable<ObjectSettingEntry>>([]);

    public Task SaveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => Task.CompletedTask;
    public Task RemoveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => Task.CompletedTask;
}
