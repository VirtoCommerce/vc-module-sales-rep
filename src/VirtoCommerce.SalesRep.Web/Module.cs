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
using VirtoCommerce.SalesRep.Data.Services;
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

        // The module's order search: subclasses the Orders CustomerOrderSearchService (reusing its query/hydration
        // pipeline) and adds a grouped "latest order per organization" lookup for "my customers". Registered under
        // its own interface only, so the platform-wide ICustomerOrderSearchService registration is unaffected.
        serviceCollection.AddTransient<ISalesRepCustomerOrderSearchService, SalesRepCustomerOrderSearchService>();

        // Single-sourced "primary contact of an organization" rule (owner → oldest contact), shared by the
        // customer detail card (VCST-5308) and the primary-contact recipient policy.
        serviceCollection.AddTransient<ISalesRepPrimaryContactResolver, SalesRepPrimaryContactResolver>();

        // Recipients of a Rep's customer communication (VCST-5310). Default: every member of the organization.
        // A project can change the policy (e.g. PrimaryContactRecipientResolver) with a later registration.
        serviceCollection.AddTransient<ISalesRepRecipientResolver, AllMembersRecipientResolver>();

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

        // Register the Rep→customer email notification (VCST-5331) with its default template.
        var notificationRegistrar = serviceProvider.GetRequiredService<INotificationRegistrar>();
        var notificationTemplatesPath = Path.Combine(ModuleInfo.FullPhysicalPath, "NotificationTemplates");
        notificationRegistrar.RegisterNotification<SalesRepMessageEmailNotification>().WithTemplatesFromPath(notificationTemplatesPath);

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
