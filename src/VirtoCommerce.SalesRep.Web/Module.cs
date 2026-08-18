using System;
using System.IO;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.FileExperienceApi.Core.Authorization;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.NotificationsModule.TemplateLoader.FileSystem;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.MySql.Extensions;
using VirtoCommerce.Platform.Data.PostgreSql.Extensions;
using VirtoCommerce.Platform.Data.SqlServer.Extensions;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Notifications;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.SalesRep.Data.MySql;
using VirtoCommerce.SalesRep.Data.PostgreSql;
using VirtoCommerce.SalesRep.Data.Repositories;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Data.Services.Statistics;
using VirtoCommerce.SalesRep.Data.Validation;
using VirtoCommerce.SalesRep.Data.SqlServer;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.SalesRep.ExperienceApi.Authorization;
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
        serviceCollection.AddDbContext<SalesRepDbContext>(options =>
        {
            var databaseProvider = Configuration.GetValue("DatabaseProvider", "SqlServer");
            var connectionString = Configuration.GetConnectionString(ModuleInfo.Id) ?? Configuration.GetConnectionString("VirtoCommerce");

            switch (databaseProvider)
            {
                case "MySql":
                    options.UseMySqlDatabase(connectionString, typeof(MySqlDataAssemblyMarker), Configuration);
                    break;
                case "PostgreSql":
                    options.UsePostgreSqlDatabase(connectionString, typeof(PostgreSqlDataAssemblyMarker), Configuration);
                    break;
                default:
                    options.UseSqlServerDatabase(connectionString, typeof(SqlServerDataAssemblyMarker), Configuration);
                    break;
            }
        });

        serviceCollection.AddTransient<ISalesRepRepository, SalesRepRepository>();
        serviceCollection.AddSingleton<Func<ISalesRepRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<ISalesRepRepository>());

        serviceCollection.AddTransient<AbstractValidator<SalesRepDocumentMetadata>, SalesRepDocumentMetadataValidator>();
        serviceCollection.AddTransient<ISalesRepDocumentMetadataService, SalesRepDocumentMetadataService>();
        serviceCollection.AddTransient<ISalesRepDocumentMetadataSearchService, SalesRepDocumentMetadataSearchService>();
        serviceCollection.AddTransient<ISalesRepDocumentService, SalesRepDocumentService>();
        serviceCollection.AddTransient<ISalesRepDocumentSearchService, SalesRepDocumentSearchService>();

        // One fail-closed handler for every document surface: the GraphQL builders run it directly, the factory
        // routes the generic file surfaces (GET /api/files/{id}, deleteFile) to it.
        serviceCollection.AddSingleton<IFileAuthorizationRequirementFactory, SalesRepDocumentAuthorizationRequirementFactory>();
        serviceCollection.AddSingleton<IAuthorizationHandler, SalesRepDocumentAuthorizationHandler>();

        serviceCollection.AddTransient<ISalesRepRoleResolver, SalesRepRoleResolver>();
        serviceCollection.AddTransient<ISalesRepRoleSeeder, SalesRepRoleSeeder>();
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

        using (var serviceScope = serviceProvider.CreateScope())
        {
            using var dbContext = serviceScope.ServiceProvider.GetRequiredService<SalesRepDbContext>();
            dbContext.Database.Migrate();
        }

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

        // Seed the default roles once (create-if-none, matched by permission set). No distributed lock is needed:
        // PostInitialize runs inside the platform's startup critical section (ExecuteSynchronized(nameof(Startup))),
        // which already serializes it across instances, so the "two instances both create one" race can't occur.
        using var scope = serviceProvider.CreateScope();
        var roleResolver = scope.ServiceProvider.GetRequiredService<ISalesRepRoleResolver>();
        var roleSeeder = scope.ServiceProvider.GetRequiredService<ISalesRepRoleSeeder>();
#pragma warning disable S4462 // one-shot startup seeding in a sync PostInitialize — the platform's standard pattern
        roleResolver.EnsureSalesRepRoleAsync().GetAwaiter().GetResult();
        roleSeeder.EnsureDocumentRolesAsync().GetAwaiter().GetResult();
#pragma warning restore S4462
    }

    public void Uninstall()
    {
    }
}
