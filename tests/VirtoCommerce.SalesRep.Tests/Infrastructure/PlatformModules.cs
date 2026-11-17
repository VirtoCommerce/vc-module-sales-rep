using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using VirtoCommerce.AssetsModule.Core.Services;
using VirtoCommerce.FileSystemAssetsModule.Core.Extensions;
using VirtoCommerce.LuceneSearchModule.Data;
using VirtoCommerce.NotificationsModule.LiquidRenderer;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Data.Extensions;
using VirtoCommerce.Platform.Data.Repositories;
using VirtoCommerce.Platform.DistributedLock;
using VirtoCommerce.Platform.DistributedLock.InProcess;
using VirtoCommerce.Platform.Modules;
using VirtoCommerce.Platform.Security;
using VirtoCommerce.Platform.Security.Authorization;
using VirtoCommerce.Platform.Security.Repositories;
using VirtoCommerce.Platform.Security.Services;
using VirtoCommerce.SearchModule.Core.Extensions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.SearchModule.Data.SearchPhraseParsing;
using VirtoCommerce.SearchModule.Data.Services;
using PlatformSettings = VirtoCommerce.Platform.Core.PlatformConstants.Settings;
using SearchSettings = VirtoCommerce.SearchModule.Core.ModuleConstants.Settings;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

/// <summary>
/// The platform and the modules the harnesses host, registered as Platform.Web's <c>Startup</c> and each module's own
/// <c>Module.Initialize</c> register them (at the package versions the test project references), with the parts no test
/// path reaches left out: export and import, bulk actions, background jobs and the event handlers that only enqueue
/// one, admin authorization. Every database is an in-memory SQLite one (<see cref="ModuleDatabases"/>) and the search
/// index is Lucene's, in RAM. A module's <c>Module.cs</c> ships in its zip, not on NuGet, so what it registers is
/// restated here from its source at the referenced version; where a module exposes its registration as an extension
/// (the platform's, X-API's, X-Catalog's, X-Cart's, X-Order's), that extension is called. The module under test is not
/// restated: its own <c>Module.Initialize</c> and <c>Module.PostInitialize</c> run (<see cref="TestedModule"/>).
/// </summary>
/// <remarks>
/// Process-wide statics (type factory registrations, GraphQL's naming switches, the audit triggers, the notification
/// script object) are set once per process, as the platform sets them once per start: <see cref="PlatformStatics"/>.
/// </remarks>
internal static partial class PlatformModules
{
    public const string SearchProviderName = "Lucene";

    /// <summary>The assets' public URL, as appsettings' Assets:FileSystem:PublicUrl sets it: nothing reads a file from it.</summary>
    public const string AssetsPublicUrl = "http://localhost/assets";

    /// <summary>
    /// The modules' assemblies, loaded once, as the platform loads every installed module's at startup before the modules
    /// register: X-API finds a module's optional graph types (X-Frontend's white labeling ones) among the loaded ones.
    /// </summary>
    private static readonly Lazy<Assembly[]> _moduleAssemblies = new(() => Directory
        .GetFiles(AppContext.BaseDirectory, "VirtoCommerce.*.dll")
        .Select(path => Assembly.Load(AssemblyName.GetAssemblyName(path)))
        .ToArray());

    /// <summary>Everything the harnesses host, over <paramref name="configuration"/>.</summary>
    /// <param name="hosted">Whether the optional task management module is installed, and where the assets go.</param>
    public static IServiceCollection AddPlatformModules(this IServiceCollection services, IConfiguration configuration, HostedModules hosted)
    {
        _ = _moduleAssemblies.Value;

        services
            .AddPlatform(configuration)
            .AddSingleton<IModuleService>(new InstalledModuleService(hosted.TaskManagement))
            // Platform.Web's: the apps and storefront plugins of the installed modules (X-API's store.plugins).
            .AddSingleton<IAppManifestService, AppManifestService>()
            .AddSecurity()
            .AddSearchModule()
            .AddSeoModule()
            .AddStoreModule()
            .AddCoreModule()
            .AddCustomerModule()
            .AddCatalogModule()
            .AddPricingModule()
            .AddMarketingModule()
            .AddAssetsModule(hosted.AssetsRootPath, configuration)
            .AddInventoryModule()
            .AddPaymentModule()
            .AddShippingModule()
            .AddCartModule()
            .AddOrdersModule(configuration)
            .AddNotificationsModule(configuration)
            .AddPushMessagesModule(configuration)
            .AddXapiModule(configuration)
            .AddXCatalogModule()
            .AddXCartModule()
            .AddXOrderModule();

        if (hosted.TaskManagement)
        {
            services.AddTaskManagementModule();
        }

        return services;
    }

    /// <summary>
    /// A module's database: the options a module builds from its connection string give way to SQLite.
    /// <paramref name="configure"/> adapts the model where SQLite needs it (see <see cref="ModuleDatabases.Create{TContext}"/>).
    /// </summary>
    public static IServiceCollection AddModuleDatabase<TContext>(this IServiceCollection services, Action<DbContextOptionsBuilder> configure = null)
        where TContext : DbContext
    {
        services.RemoveAll<DbContextOptions<TContext>>();
        services.AddSingleton(provider => provider.GetRequiredService<ModuleDatabases>().Create<TContext>(configure));
        services.TryAddScoped<TContext>();

        return services;
    }

    /// <summary>
    /// The platform's own services (Platform.Data's <c>AddPlatformServices</c>: settings, localizable settings, dynamic
    /// properties, localized items, the in-process event bus, the change log, the memory and request caches, countries,
    /// translations), over its database; what Platform.Web's Startup adds around them (the HTTP client factory, the MVC
    /// JSON options, optional module dependencies); the distributed lock and its service as the platform registers them
    /// without Redis.
    /// </summary>
    private static IServiceCollection AddPlatform(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLogging();
        services.AddHttpClient();
        services.AddHttpContextAccessor();
        services.AddSingleton<SqlCommandLog>();
        services.AddSingleton<ModuleDatabases>();

        services.AddOptions<PlatformOptions>().Bind(configuration.GetSection("VirtoCommerce"));
        services.AddOptions<MvcNewtonsoftJsonOptions>();
        services.AddModuleDatabase<PlatformDbContext>();
        services.AddPlatformServices(configuration);
        services.AddOptions<FixedSettings>();

        services.Add(ServiceDescriptor.Singleton(typeof(IOptionalDependency<>), typeof(OptionalDependencyManager<>)));

        services.AddOptions<DistributedLockOptions>();
        services.AddSingleton<IDistributedLock, InProcessDistributedLock>();
        services.AddSingleton<IDistributedLockService, DistributedLockServiceAdapter>();

        return services;
    }

    /// <summary>
    /// Accounts and roles, as Platform.Web registers them: its <c>AddSecurityServices</c> (the platform's custom
    /// Identity stack, two-factor sign-in, the user and role searches), then ASP.NET Identity over the security database.
    /// The data protection keys live in memory. Left out with the OpenIddict token server: API keys, sign-in logs and
    /// the token validators.
    /// </summary>
    private static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        // ASP.NET Identity models LockoutEnd as a DateTimeOffset, which SQLite can't compare (OnlyUnlocked searches do).
        services.AddModuleDatabase<SecurityDbContext>(options => options.ReplaceService<IModelCustomizer, LockoutEndSqliteModelCustomizer>());
        services.AddTransient<ISecurityRepository, SecurityRepository>();
        services.AddTransient<Func<ISecurityRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetService<ISecurityRepository>());

        services.AddScoped<IUserNameResolver, HttpContextUserResolver>();
        services.AddSingleton<IPermissionsRegistrar, DefaultPermissionProvider>();
        services.AddScoped<IRoleSearchService, RoleSearchService>();
        services.AddSingleton<IUserSearchService>(provider =>
        {
            var scope = provider.CreateScope();
            return new UserSearchService(scope.ServiceProvider.GetService<Func<UserManager<ApplicationUser>>>(),
                                         scope.ServiceProvider.GetService<Func<RoleManager<Role>>>());
        });

        services.TryAddScoped<RoleManager<Role>, CustomRoleManager>();
        services.TryAddScoped<UserManager<ApplicationUser>, CustomUserManager>();
        services.TryAddScoped<SignInManager<ApplicationUser>, VirtoSignInManager>();
        services.TryAddScoped<IPasswordValidator<ApplicationUser>, CustomPasswordValidator>();
        services.TryAddScoped<IdentityErrorDescriber, CustomIdentityErrorDescriber>();
        services.TryAddScoped<IUserStore<ApplicationUser>, CustomUserStore>();
        services.AddSingleton<Func<RoleManager<Role>>>(provider => () => provider.CreateScope().ServiceProvider.GetService<RoleManager<Role>>());
        services.AddSingleton<Func<UserManager<ApplicationUser>>>(provider => () => provider.CreateScope().ServiceProvider.GetService<UserManager<ApplicationUser>>());
        services.AddSingleton<Func<SignInManager<ApplicationUser>>>(provider => () => provider.CreateScope().ServiceProvider.GetService<SignInManager<ApplicationUser>>());
        services.TryAddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, CustomUserClaimsPrincipalFactory>();
        services.AddScoped<ITwoFactorSignInService, TwoFactorSignInService>();

        // Startup's factory for the account sessions service, which X-Profile's password commands take. The service
        // itself revokes OpenIddict tokens, and the token server is not hosted (TestTokenService): no path here signs
        // anyone out of their sessions.
        services.AddSingleton<Func<(IUserSessionsService SessionService, IServiceScope Scope)>>(provider => () =>
        {
            var scope = provider.CreateScope();
            return (scope.ServiceProvider.GetRequiredService<IUserSessionsService>(), scope);
        });

        services.AddIdentity<ApplicationUser, Role>(options => options.Stores.MaxLengthForKeys = 128)
            .AddEntityFrameworkStores<SecurityDbContext>()
            .AddDefaultTokenProviders()
            .AddUserValidator<CustomUserValidator>();

        // The JWT claim types OpenIddict issues, which the platform makes Identity's (and the current user's).
        services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.UserNameClaimType = OpenIddictConstants.Claims.Name;
            options.ClaimsIdentity.UserIdClaimType = OpenIddictConstants.Claims.Subject;
            options.ClaimsIdentity.RoleClaimType = OpenIddictConstants.Claims.Role;
            options.ClaimsIdentity.EmailClaimType = OpenIddictConstants.Claims.Email;

            ClaimsPrincipalExtensions.UserIdClaimTypes = [options.ClaimsIdentity.UserIdClaimType, ClaimTypes.NameIdentifier];
            ClaimsPrincipalExtensions.UserNameClaimTypes = [options.ClaimsIdentity.UserNameClaimType];
        });
        services.AddOptions<PasswordOptionsExtended>();
        services.AddOptions<LockoutOptionsExtended>();
        services.AddOptions<UserOptionsExtended>();
        services.AddOptions<PasswordLoginOptions>();

        services.AddDataProtection().UseEphemeralDataProtectionProvider();

        // Platform.Web's authorization (its permission policy provider, by permission name, is the host's own): the
        // permission requirement handler; the modules add their own handlers.
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationHandler, DefaultPermissionAuthorizationHandler>();

        return services;
    }

    /// <summary>The search module and the Lucene provider, which its gateway serves.</summary>
    private static IServiceCollection AddSearchModule(this IServiceCollection services)
    {
        services.AddSingleton(Options.Create(new SearchOptions { Provider = SearchProviderName, Scope = "test" }));
        services.AddSingleton<DummySearchProvider>();
        services.AddSingleton<SearchGateway>();
        services.AddSingleton<ISearchGateway>(provider => provider.GetRequiredService<SearchGateway>());
        services.AddSingleton<ISearchProvider>(provider => provider.GetRequiredService<SearchGateway>());
        services.AddTransient<ISearchPhraseParser, SearchPhraseParser>();
        services.AddSingleton<IIndexingManager, IndexingManager>();
        services.AddSingleton<ISearchRequestBuilderRegistrar, SearchRequestBuilderRegistrar>();
        services.AddTransient<IIndexFieldSettingService, IndexFieldSettingService>();
        services.AddTransient<IIndexFieldSettingSearchService, IndexFieldSettingService>();
        services.AddTransient<IIndexDocumentConverter, FieldValueConverter>();

        services.AddSingleton(Options.Create(new LuceneSearchOptions { UseInMemory = true }));
        services.AddSingleton<LuceneSearchProvider>();

        return services;
    }

    /// <summary>
    /// What Platform.Web's <c>Startup.Configure</c> and each hosted module's <c>PostInitialize</c> do at startup: register
    /// their settings (an unregistered setting can't be read), permissions, notifications, dynamic property and member
    /// types, search request builders and event handlers, register the payment and shipping methods, serve the search
    /// providers and map the GraphQL endpoints. <paramref name="app"/> is the web host's in the E2E hosts and one over the
    /// harness's provider in the component tests. The module under test's own PostInitialize runs after this.
    /// </summary>
    public static async Task PostInitializeAsync(IApplicationBuilder app, HostedModules hosted)
    {
        var provider = app.ApplicationServices;
        var settings = provider.GetRequiredService<ISettingsRegistrar>();

        // Platform.Web's UsePlatformSettings.
        settings.RegisterSettings(PlatformSettings.AllSettings, "Platform");
        settings.RegisterSettingsForType(PlatformSettings.UserProfile.AllSettings, nameof(PlatformSettings.UserProfile));
        provider.GetRequiredService<IPermissionsRegistrar>().RegisterPermissions(PlatformConstants.Security.Permissions.AllPermissions
            .Select(x => new Permission { GroupName = "Platform", Name = x })
            .ToArray());

        settings.RegisterSettings(SearchSettings.AllSettings, "VirtoCommerce.Search");
        settings.RegisterSettings(provider.GetServices<IndexDocumentConfiguration>()
            .Select(x => x.DocumentType)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(SearchSettings.IndexingJobs.IndexationDate)
            .ToArray(), "VirtoCommerce.Search");
        app.UseSearchProvider<DummySearchProvider>(name: null);
        app.UseSearchProvider<LuceneSearchProvider>(SearchProviderName);

        PostInitializeCommerceModules(app);
        await PostInitializeSearchSourcesAsync(provider);
        PostInitializeOrderModules(app, hosted);
        PostInitializeExperienceApiModules(app);
    }

    /// <summary>
    /// The process's own platform services (<see cref="PlatformStatics.Services"/>): the platform's (settings, dynamic
    /// properties, translations, which read the installed modules' translation files) over a database of its own, the
    /// assets' URL resolver (the file system blob provider with every harness's public URL) and the notification script
    /// object they make.
    /// </summary>
    public static IServiceProvider BuildProcessServices()
    {
        var configuration = new ConfigurationBuilder().Build();

        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddPlatform(configuration)
            .AddSingleton<IModuleService>(new InstalledModuleService(taskManagementInstalled: true))
            .AddSingleton<IFileExtensionService, FileExtensionService>()
            .AddTransient<NotificationScriptObject>();
        services.AddFileSystemBlobProvider(options =>
        {
            options.RootPath = Path.Combine(Path.GetTempPath(), "VirtoCommerce.SalesRep.Tests", "script-object-assets");
            options.PublicUrl = AssetsPublicUrl;
        });

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// What the modules do once the application has started (the platform's ApplicationStarted): the notifications
    /// module saves every registered notification, which is where the notification searches read them from. Before that,
    /// the notifications the modules registered, complete by now, go to the type factory (<see cref="HarnessNotificationRegistrar"/>).
    /// </summary>
    public static Task StartAsync(IServiceProvider provider)
    {
        provider.GetRequiredService<HarnessNotificationRegistrar>().Publish();

        return SaveRegisteredNotificationsAsync(provider);
    }
}
