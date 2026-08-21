using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.NotificationsModule.TemplateLoader.FileSystem;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Notifications;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Data.Services.Statistics;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Schemas;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XCart.Core.Schemas;
using VirtoCommerce.XCart.Core.Services;

namespace VirtoCommerce.SalesRep.Web;

public class Module : IModule, IHasConfiguration
{
    public ManifestModuleInfo ModuleInfo { get; set; }
    public IConfiguration Configuration { get; set; }

    public void Initialize(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<ISalesRepMapper, SalesRepMapper>();
        serviceCollection.AddTransient<ISalesRepRoleResolver, SalesRepRoleResolver>();
        serviceCollection.AddTransient<ISalesRepOrganizationAccessService, SalesRepOrganizationAccessService>();
        serviceCollection.AddTransient<ISalesRepService, SalesRepService>();
        serviceCollection.AddTransient<ISalesRepSearchService, SalesRepSearchService>();
        serviceCollection.AddTransient<ISalesRepDictionaryService, SalesRepDictionaryService>();

        serviceCollection.AddTransient<ISalesRepCustomerOrderSearchService, SalesRepCustomerOrderSearchService>();

        serviceCollection.AddTransient<ISalesRepOrderStatusService, SalesRepOrderStatusService>();

        serviceCollection.AddTransient<ICustomerOrderStatisticsService, CustomerOrderStatisticsService>();

        serviceCollection.AddTransient<ICustomerCartStatisticsService, CustomerCartStatisticsService>();

        serviceCollection.AddTransient<ISalesRepCustomerCountsService, SalesRepCustomerCountsService>();

        serviceCollection.AddTransient<ISalesRepTopSellerService, SalesRepTopSellerService>();

        serviceCollection.AddTransient<ISalesRepPrimaryContactResolver, SalesRepPrimaryContactResolver>();

        serviceCollection.AddTransient<ILayoutService, LayoutService>();

        serviceCollection.AddTransient<ISalesRepRecipientResolver, AllMembersRecipientResolver>();

        // VCST-5332: teach the XCart sharing pipeline the "Customer" wishlist scope. Registered after XCart
        // (SalesRep depends on it), so this override wins for ICartSharingService; the enum override lets the
        // new scope value serialize on the core /graphql wishlist schema.
        serviceCollection.AddTransient<ICartSharingService, SalesRepCartSharingService>();
        serviceCollection.OverrideGraphType<WishlistScopeType, SalesRepWishlistScopeType>();

        serviceCollection.AddSalesRepExperienceApi();
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        var serviceProvider = appBuilder.ApplicationServices;

        var settingsRegistrar = serviceProvider.GetRequiredService<ISettingsRegistrar>();
        settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);
        // "Store" is nameof(StoreModule's Store entity), inlined as a literal to avoid a StoreModule dependency just
        // for the type name. Only the public General settings are per-store; the cache TTLs stay module-global.
        settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.General.AllGeneralSettings, "Store");

        var permissionsRegistrar = serviceProvider.GetRequiredService<IPermissionsRegistrar>();
        permissionsRegistrar.RegisterPermissions(ModuleInfo.Id, "Sales Rep", ModuleConstants.Security.Permissions.AllPermissions);

        var notificationRegistrar = serviceProvider.GetRequiredService<INotificationRegistrar>();
        var notificationTemplatesPath = Path.Combine(ModuleInfo.FullPhysicalPath, "NotificationTemplates");
        notificationRegistrar.RegisterNotification<SalesRepMessageEmailNotification>().WithTemplatesFromPath(notificationTemplatesPath);

        appBuilder.UseScopedSchema<XapiAssemblyMarker>("sales-rep");

        // Seed the default "Sales Representative" role once (create-if-none). No distributed lock is needed:
        // PostInitialize runs inside the platform's startup critical section (ExecuteSynchronized(nameof(Startup))),
        // which already serializes it across instances, so the "two instances both create one" race can't occur.
        using var scope = serviceProvider.CreateScope();
        var roleResolver = scope.ServiceProvider.GetRequiredService<ISalesRepRoleResolver>();
#pragma warning disable S4462 // one-shot startup seeding in a sync PostInitialize — the platform's standard pattern
        roleResolver.EnsureSalesRepRoleAsync().GetAwaiter().GetResult();
#pragma warning restore S4462
    }

    public void Uninstall()
    {
    }
}
