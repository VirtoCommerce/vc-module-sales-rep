using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Data.Services.Statistics;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.Web;

public class Module : IModule, IHasConfiguration
{
    public ManifestModuleInfo ModuleInfo { get; set; }
    public IConfiguration Configuration { get; set; }

    public void Initialize(IServiceCollection serviceCollection)
    {
        // This module owns no tables — it composes existing Member / ApplicationUser / OrganizationMembership data.
        serviceCollection.AddTransient<ISalesRepRoleResolver, SalesRepRoleResolver>();
        serviceCollection.AddTransient<ISalesRepService, SalesRepService>();
        serviceCollection.AddTransient<ISalesRepSearchService, SalesRepSearchService>();
        serviceCollection.AddTransient<ISalesRepDictionaryService, SalesRepDictionaryService>();

        // The module's order search: subclasses the Orders CustomerOrderSearchService (reusing its query/hydration
        // pipeline) and adds a grouped "latest order per organization" lookup for "my customers". Registered under
        // its own interface only, so the platform-wide ICustomerOrderSearchService registration is unaffected.
        serviceCollection.AddTransient<ISalesRepCustomerOrderSearchService, SalesRepCustomerOrderSearchService>();

        // Customer order statistics (YTD/lifetime purchases, order count, average order value) for the customer
        // profile widgets (VCST-5309). Aggregates in the DB via the Orders repository and converts to one currency.
        serviceCollection.AddTransient<ICustomerOrderStatisticsService, CustomerOrderStatisticsService>();

        // Cart/project statistics (dashboard "Active Projects" and related cart widgets). Aggregates in the DB via
        // the Cart repository and converts to one currency, mirroring the order statistics service.
        serviceCollection.AddTransient<ICustomerCartStatisticsService, CustomerCartStatisticsService>();

        // "My customers" counters (dashboard "My Customers" widget): customers who ordered in a period and customers
        // new in a period, derived from the rep's own orders via the Orders repository.
        serviceCollection.AddTransient<ISalesRepCustomerCountsService, SalesRepCustomerCountsService>();

        // Storefront X-API (GraphQL) surface: "my customers" (VCST-5304) and "my sales reps" (VCST-4907).
        serviceCollection.AddSalesRepExperienceApi();
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        var serviceProvider = appBuilder.ApplicationServices;

        // Register settings
        var settingsRegistrar = serviceProvider.GetRequiredService<ISettingsRegistrar>();
        settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);
        // Also register per store so the storefront can read the public SalesRep.Enabled flag from
        // store.settings.modules and toggle the Sales Rep UI per store. ("Store" == nameof(StoreModule's Store
        // entity); used as a literal to avoid a StoreModule dependency just for the type name.)
        settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.AllSettings, "Store");

        // Register permissions
        var permissionsRegistrar = serviceProvider.GetRequiredService<IPermissionsRegistrar>();
        permissionsRegistrar.RegisterPermissions(ModuleInfo.Id, "Sales Rep", ModuleConstants.Security.Permissions.AllPermissions);

        // Expose the storefront X-API queries on their own GraphQL endpoint: /graphql/sales-rep (+ /ui/graphiql/sales-rep).
        appBuilder.UseScopedSchema<XapiAssemblyMarker>("sales-rep");

        // Seed the default "Sales Representative" role once, right after its permission is registered — but only
        // if no role already grants sales-rep:access (EnsureSalesRepRoleAsync is create-if-none). No explicit
        // distributed lock is needed: PostInitialize runs inside the platform's startup critical section
        // (Startup.Configure -> app.ExecuteSynchronized(nameof(Startup)) -> PostInitializeModules), which
        // already serializes this across instances, so the "two instances both create one" race can't occur.
        using var scope = serviceProvider.CreateScope();
        var roleResolver = scope.ServiceProvider.GetRequiredService<ISalesRepRoleResolver>();
#pragma warning disable S4462 // one-shot startup seeding in a sync PostInitialize — the platform's standard pattern
        roleResolver.EnsureSalesRepRoleAsync().GetAwaiter().GetResult();
#pragma warning restore S4462
    }

    public void Uninstall()
    {
        // Nothing to do here
    }
}
