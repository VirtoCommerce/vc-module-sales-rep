using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.DistributedLock;
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

        // Seed the default "Sales Representative" role once, right after its permission is registered — but
        // only if no role already grants sales-rep:access (EnsureSalesRepRoleAsync is create-if-none). Seeding
        // here rather than lazily on a GET keeps read endpoints side-effect-free. Admins may later rename or
        // delete it and substitute their own granting role.
        //
        // The seed runs under a cluster-wide distributed lock so that, when several instances boot at once,
        // only one performs the create; the others wait for it (tryLockTimeout), then re-check and find the
        // role — no duplicate. With Redis configured this is a real lock; without Redis the platform registers
        // a no-op lock, which is fine for a single instance where no race exists.
        using var scope = serviceProvider.CreateScope();
        var lockService = scope.ServiceProvider.GetRequiredService<IDistributedLockService>();
        var roleResolver = scope.ServiceProvider.GetRequiredService<ISalesRepRoleResolver>();
        lockService.ExecuteAsync(
            "SalesRep:SeedDefaultRole",
            () => roleResolver.EnsureSalesRepRoleAsync(),
            // tryLockTimeout + retryInterval must BOTH be set to make a contending instance wait-and-retry;
            // omitting either falls back to a single-shot acquire that throws immediately if the lock is held.
            tryLockTimeout: TimeSpan.FromSeconds(15),
            retryInterval: TimeSpan.FromSeconds(1))
            .GetAwaiter().GetResult();
    }

    public void Uninstall()
    {
        // Nothing to do here
    }
}
