using System.Collections.Generic;
using System.IO;
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
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Model.Search;
using VirtoCommerce.StoreModule.Core.Services;
using TaskManagementSettings = VirtoCommerce.TaskManagement.Core.ModuleConstants.Settings.General;
using XapiModuleConstants = VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

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
/// The module list the store query reports to the storefront, and the one the platform's AppManifestService walks for
/// store.plugins. The module under test comes from its own module.manifest, the file the platform reads, so its id,
/// version and apps are the real ones; two of its dependencies stand in. Task management opens the storefront's tasks
/// pages (a presence check on the module id, the way the real module list reaches it with ReturnModuleVersion on), and
/// a group can leave it out; X-Frontend declares the storefront app whose plugins store.plugins lists. White labeling
/// is absent so the storefront drops its @gate'd field client-side.
/// </summary>
internal sealed class StorefrontModuleService(bool taskManagementInstalled) : IModuleService
{
    /// <summary>
    /// What the two stand-ins report: a version no storefront expects to exceed. The storefront compares each module's
    /// version with its own backend-packages.json, and a lower one gets its "outdated backend" error over the page's
    /// top-right buttons for fifteen seconds; a built storefront keeps that list inside its bundle. The module under
    /// test reports its real version, so a real mismatch still shows.
    /// </summary>
    private const string StandInVersion = "999.0.0";

    private readonly IList<ManifestModuleInfo> _installed = CreateInstalledModules(taskManagementInstalled);

    public IList<ManifestModuleInfo> GetModules() => _installed;
    public IList<ManifestModuleInfo> GetInstalledModules() => _installed;
    public IList<ManifestModuleInfo> GetFailedModules() => [];
    public bool IsInstalled(string moduleId) => GetModule(moduleId) != null;
    public bool IsInstalled(string moduleId, string minVersion) => IsInstalled(moduleId);
    public ManifestModuleInfo GetModule(string moduleId) => _installed.FirstOrDefault(x => x.Id.EqualsIgnoreCase(moduleId));

    private static List<ManifestModuleInfo> CreateInstalledModules(bool taskManagementInstalled)
    {
        var manifest = ManifestReader.Read(Path.Combine(StorefrontPlugin.ModuleDirectory, "module.manifest"));

        var salesRep = new ManifestModuleInfo().LoadFromManifest(manifest);
        // Where the platform looks for the hub plugin's build (plugins/vc-frontend).
        salesRep.FullPhysicalPath = StorefrontPlugin.ModuleDirectory;

        List<ManifestModuleInfo> modules =
        [
            salesRep,
            // X-Frontend's own manifest ships only in its module zip, so its app declaration is restated here.
            StandIn(manifest, "VirtoCommerce.XFrontend", new ManifestApp { Id = "vc-frontend" }),
        ];

        if (taskManagementInstalled)
        {
            modules.Add(StandIn(manifest, "VirtoCommerce.TaskManagement"));
        }

        return modules;
    }

    private static ManifestModuleInfo StandIn(ModuleManifest manifest, string id, params ManifestApp[] apps)
        => new ManifestModuleInfo().LoadFromManifest(new ModuleManifest { Id = id, Version = StandInVersion, PlatformVersion = manifest.PlatformVersion, Apps = apps });
}

/// <summary>
/// The harness's minimal settings double plus the storefront-relevant values: ReturnModuleVersion on, so the store
/// query appends the installed modules without public settings (task management) to store.settings.modules, and the
/// task types the hub's task form offers.
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

        if (name == TaskManagementSettings.TaskTypes.Name)
        {
            // The built-in list with "Call" added, as the module's Initialize does - on a copy, since the descriptor
            // is task management's static one.
            var taskTypes = new SettingDescriptor { Name = name, AllowedValues = TaskManagementSettings.TaskTypes.AllowedValues.ToArray() };
            SalesRepTaskTypeSeeder.AddCallType(taskTypes);
            setting.AllowedValues = taskTypes.AllowedValues;
        }

        return Task.FromResult(setting);
    }

    public Task<IEnumerable<ObjectSettingEntry>> GetObjectSettingsAsync(IEnumerable<string> names, string objectType = null, string objectId = null)
        => Task.FromResult<IEnumerable<ObjectSettingEntry>>([]);

    public Task SaveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => Task.CompletedTask;
    public Task RemoveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => Task.CompletedTask;
}
