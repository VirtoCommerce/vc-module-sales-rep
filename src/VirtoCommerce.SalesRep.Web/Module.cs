using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;

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
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        var serviceProvider = appBuilder.ApplicationServices;

        // Register settings
        var settingsRegistrar = serviceProvider.GetRequiredService<ISettingsRegistrar>();
        settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

        // Register permissions
        var permissionsRegistrar = serviceProvider.GetRequiredService<IPermissionsRegistrar>();
        permissionsRegistrar.RegisterPermissions(ModuleInfo.Id, "Sales Rep", ModuleConstants.Security.Permissions.AllPermissions);

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
