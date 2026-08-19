using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.AssetsModule.Core.Services;
using VirtoCommerce.AssetsModule.Data.Repositories;
using VirtoCommerce.AssetsModule.Data.Services;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.FileExperienceApi.Core.Models;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.FileExperienceApi.Data.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.DistributedLock;
using VirtoCommerce.Platform.DistributedLock.NoLock;
using VirtoCommerce.SalesRep.Data.Repositories;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.CustomerModule.Core.Services.Indexed;
using VirtoCommerce.CustomerModule.Data.Handlers;
using VirtoCommerce.CustomerModule.Data.Repositories;
using VirtoCommerce.CustomerModule.Data.Search;
using VirtoCommerce.CustomerModule.Data.Search.Indexing;
using VirtoCommerce.CustomerModule.Data.Services;
using VirtoCommerce.Platform.Core.DynamicProperties;
using VirtoCommerce.CustomerModule.Data.Validation;
using VirtoCommerce.LuceneSearchModule.Data;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Bus;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.Platform.Data.Common;
using VirtoCommerce.Platform.Security;
using VirtoCommerce.Platform.Security.Repositories;
using VirtoCommerce.Platform.Security.Services;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Handlers;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Data.Validation;
using VirtoCommerce.SalesRep.Web.Controllers.Api;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.SearchModule.Data.SearchPhraseParsing;
using VirtoCommerce.SearchModule.Data.Services;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using CustomerSettings = VirtoCommerce.CustomerModule.Core.ModuleConstants.Settings.General;
using SalesRepModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Registers the REAL platform/customer/sales-rep services against in-memory SQLite for component tests
/// (ported from the modules' Module.Initialize/PostInitialize — the .Web assemblies aren't referenceable).
/// Built up incrementally; the security slice is validated first.
/// </summary>
internal static class TestServicesConfiguration
{
    /// <summary>
    /// Platform security on SQLite: real Identity with VC's custom stack (CustomUserStore/CustomUserManager/
    /// CustomRoleManager), real in-process event bus, and IUserSearchService — mirrors the platform Startup
    /// (AddIdentity + AddEntityFrameworkStores) followed by AddSecurityServices' custom overrides.
    /// </summary>
    public static IServiceCollection AddSecuritySlice(this IServiceCollection services, DbContextOptions<SecurityDbContext> securityDbOptions)
    {
        // Platform infra
        services.AddLogging();
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.Configure<CachingOptions>(_ => { });
        services.Configure<UserOptionsExtended>(_ => { });
        services.Configure<PasswordOptionsExtended>(_ => { });
        services.Configure<MvcNewtonsoftJsonOptions>(_ => { });
        services.AddSingleton<IPlatformMemoryCache, PlatformMemoryCache>();

        // Real in-process event bus (drives the customer delete-cascade handler later)
        services.AddSingleton<InProcessBus>();
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<InProcessBus>());
        services.AddSingleton<IEventHandlerRegistrar>(sp => sp.GetRequiredService<InProcessBus>());

        // Security DB (schema already materialized on the shared connection by the options factory)
        services.AddSingleton(securityDbOptions);
        services.AddScoped<SecurityDbContext>();
        services.AddScoped<ISecurityRepository, SecurityRepository>();
        services.AddSingleton<Func<ISecurityRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<ISecurityRepository>());

        // Identity
        services.AddIdentity<ApplicationUser, Role>(options => options.Stores.MaxLengthForKeys = 128)
            .AddEntityFrameworkStores<SecurityDbContext>()
            .AddDefaultTokenProviders();

        // VC custom overrides — explicit AddScoped so they win over AddIdentity's registrations
        services.AddScoped<IUserStore<ApplicationUser>, CustomUserStore>();
        services.AddScoped<RoleManager<Role>, CustomRoleManager>();
        services.AddScoped<UserManager<ApplicationUser>, CustomUserManager>();
        services.AddScoped<IdentityErrorDescriber, CustomIdentityErrorDescriber>();
        services.AddSingleton<IPermissionsRegistrar, DefaultPermissionProvider>();

        services.AddSingleton<Func<UserManager<ApplicationUser>>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
        services.AddSingleton<Func<RoleManager<Role>>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<RoleManager<Role>>());
        services.AddSingleton<IUserSearchService>(sp =>
        {
            var scope = sp.CreateScope();
            return new UserSearchService(
                scope.ServiceProvider.GetRequiredService<Func<UserManager<ApplicationUser>>>(),
                scope.ServiceProvider.GetRequiredService<Func<RoleManager<Role>>>());
        });

        return services;
    }

    /// <summary>
    /// Customer domain on SQLite: real Member and OrganizationMembership services + repositories + validator +
    /// countries, and the member indexed-search chain backed by an in-memory (RAM) Lucene provider (needed to
    /// construct MemberSearchService — keyword member searches route to the index, non-keyword to the DB).
    /// </summary>
    public static IServiceCollection AddCustomerSlice(this IServiceCollection services, DbContextOptions<CustomerDbContext> customerDbOptions)
    {
        services.Configure<PlatformOptions>(_ => { });

        // Customer DB (schema materialized by the options factory)
        services.AddSingleton(customerDbOptions);
        services.AddScoped<CustomerDbContext>();
        services.AddTransient<ICustomerRepository, CustomerRepository>();
        services.AddSingleton<Func<ICustomerRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<ICustomerRepository>());
        services.AddSingleton<Func<IMemberRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<ICustomerRepository>());

        // Real countries service (Nager-backed, in memory)
        services.AddSingleton<FileSystemCountriesService>();
        services.AddSingleton<ICountriesService, CountriesService>();

        // Real member validator
        services.AddTransient<AbstractValidator<Member>, MemberValidator>();

        // Member indexed-search chain over an in-memory (RAM) Lucene provider
        services.AddTransient<ISearchPhraseParser, SearchPhraseParser>();
        services.AddSingleton<ISearchRequestBuilderRegistrar, SearchRequestBuilderRegistrar>();
        services.AddSingleton(Options.Create(new LuceneSearchOptions { UseInMemory = true }));
        services.AddSingleton(Options.Create(new SearchOptions { Provider = "Lucene", Scope = "test" }));
        services.AddSingleton<ISearchProvider, LuceneSearchProvider>();
        services.AddTransient<MemberSearchRequestBuilder>();
        services.AddTransient<IIndexedMemberSearchService, MemberIndexedSearchService>();

        // Member indexation: the real document builder + a no-op dynamic-property search service (dynamic-
        // property fields aren't needed for name/email keyword search). Lets tests populate the RAM index so
        // keyword member searches (which route to the index, not the DB) can be exercised.
        services.AddSingleton<IDynamicPropertySearchService, NoOpDynamicPropertySearchService>();
        services.AddSingleton<MemberDocumentBuilder>();

        // Real member + membership services
        services.AddTransient<IMemberService, MemberService>();
        services.AddTransient<IMemberSearchService, MemberSearchService>();
        services.AddSingleton<IOrganizationMembershipService, OrganizationMembershipService>();
        services.AddSingleton<IOrganizationMembershipSearchService, OrganizationMembershipSearchService>();
        services.AddSingleton<Func<IOrganizationMembershipSearchService>>(sp => () => sp.GetRequiredService<IOrganizationMembershipSearchService>());

        // CustomerPreference services (user-scoped key/value store) — back the layout persistence.
        services.AddTransient<ICustomerPreferenceCrudService, CustomerPreferenceCrudService>();
        services.AddTransient<ICustomerPreferenceSearchService, CustomerPreferenceSearchService>();
        services.AddTransient<ICustomerPreferenceService, CustomerPreferenceService>();

        // Delete-cascade handler (subscribed to the in-process bus by the harness build step)
        services.AddTransient<DeleteOrganizationMembershipUserChangedEventHandler>();

        return services;
    }

    /// <summary>
    /// Assets domain on SQLite: the REAL AssetEntry CRUD/search services + repository (the documents library's
    /// listing backbone), plus the real platform file-extension validator. Only the binary storage is a double —
    /// <see cref="InMemoryBlobStorageProvider"/> (registered as both provider and URL resolver) so tests can
    /// assert blob existence without a file system.
    /// </summary>
    public static IServiceCollection AddAssetsSlice(this IServiceCollection services, DbContextOptions<AssetsDbContext> assetsDbOptions)
    {
        services.AddSingleton(assetsDbOptions);
        services.AddScoped<AssetsDbContext>();
        services.AddTransient<IAssetsRepository, AssetsRepository>();
        services.AddSingleton<Func<IAssetsRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<IAssetsRepository>());

        services.Configure<CrudOptions>(_ => { });
        services.AddTransient<IAssetEntryService, AssetEntryService>();
        services.AddTransient<IAssetEntrySearchService, AssetEntrySearchService>();
        services.AddTransient<IFileExtensionService, FileExtensionService>();

        services.AddSingleton<InMemoryBlobStorageProvider>();
        services.AddSingleton<IBlobStorageProvider>(sp => sp.GetRequiredService<InMemoryBlobStorageProvider>());
        services.AddSingleton<IBlobUrlResolver>(sp => sp.GetRequiredService<InMemoryBlobStorageProvider>());

        // The REAL file-experience-api upload service over the same assets slice, with the documents scope
        // configured the way a deployment configures it in FileUpload:Scopes.
        services.Configure<FileUploadOptions>(options => options.Scopes.Add(new FileUploadScopeOptions
        {
            Scope = SalesRepModuleConstants.DocumentsScope,
            MaxFileSize = 50 * 1024 * 1024,
            AllowedExtensions = [],
        }));
        services.AddTransient<IFileUploadService, FileUploadService>();

        return services;
    }

    /// <summary>The module under test: real SalesRep services + the REST controller (ported from Module.Initialize).</summary>
    public static IServiceCollection AddSalesRepSlice(this IServiceCollection services, DbContextOptions<SalesRepDbContext> salesRepDbOptions)
    {
        services.AddSingleton(salesRepDbOptions);
        services.AddScoped<SalesRepDbContext>();
        services.AddTransient<ISalesRepRepository, SalesRepRepository>();
        services.AddSingleton<Func<ISalesRepRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<ISalesRepRepository>());

        services.AddTransient<AbstractValidator<SalesRepDocumentMetadata>, SalesRepDocumentMetadataValidator>();
        // The platform's non-Redis default: pass-through locking (the pin lock is exercised, not simulated).
        services.AddSingleton<IDistributedLockService, NoLockService>();
        services.AddTransient<ISalesRepDocumentMetadataService, SalesRepDocumentMetadataService>();
        services.AddTransient<ISalesRepDocumentMetadataSearchService, SalesRepDocumentMetadataSearchService>();
        services.AddTransient<ISalesRepDocumentService, SalesRepDocumentService>();
        services.AddTransient<ISalesRepDocumentSearchService, SalesRepDocumentSearchService>();
        services.AddTransient<DeleteDocumentMetadataAssetEntryChangedEventHandler>();

        services.AddTransient<ISalesRepRoleResolver, SalesRepRoleResolver>();
        services.AddTransient<ISalesRepRoleSeeder, SalesRepRoleSeeder>();
        services.AddTransient<ISalesRepOrganizationAccessService, SalesRepOrganizationAccessService>();
        services.AddTransient<ISalesRepService, SalesRepService>();
        services.AddTransient<ISalesRepSearchService, SalesRepSearchService>();
        services.AddTransient<ISalesRepDictionaryService, SalesRepDictionaryService>();
        services.AddTransient<ISalesRepPrimaryContactResolver, SalesRepPrimaryContactResolver>();
        services.AddTransient<ILayoutService, LayoutService>();
        services.AddTransient<SalesRepController>();

        // Dependencies of the dictionaries endpoint that the harness doesn't otherwise stand up. Countries is
        // already registered above; currencies and settings get lightweight doubles (the component tests don't
        // exercise dictionary contents — the controller just needs the graph to resolve).
        services.AddSingleton<ICurrencyService, TestCurrencyService>();
        services.AddSingleton<ISettingsManager, TestSettingsManager>();

        // Lightweight IStoreService double: SalesRepService reads the store's ContactDefaultStatus setting to
        // seed a rep's member status. Registered as a singleton so tests can configure per-store defaults
        // (see SalesRepTestContext.SetStoreContactDefaultStatus) without standing up a Store DbContext.
        services.AddSingleton<TestStoreService>();
        services.AddSingleton<IStoreService>(sp => sp.GetRequiredService<TestStoreService>());
        return services;
    }

    /// <summary>
    /// Minimal <see cref="IDynamicPropertySearchService"/> for member indexation — returns no dynamic-property
    /// definitions, so indexed member documents carry the standard fields (name, emails) used by keyword search.
    /// </summary>
    private sealed class NoOpDynamicPropertySearchService : IDynamicPropertySearchService
    {
        public Task<DynamicPropertySearchResult> SearchAsync(DynamicPropertySearchCriteria criteria, bool clone = true)
            => Task.FromResult(new DynamicPropertySearchResult());
    }

    /// <summary>Inert <see cref="ICurrencyService"/> double — the dictionaries endpoint resolves it, but the
    /// component tests don't assert on the currency catalog, so an empty list is enough.</summary>
    private sealed class TestCurrencyService : ICurrencyService
    {
        public Task<IEnumerable<Currency>> GetAllCurrenciesAsync() => Task.FromResult<IEnumerable<Currency>>([]);
        public Task SaveChangesAsync(Currency[] currencies) => Task.CompletedTask;
        public Task DeleteCurrenciesAsync(string[] codes) => Task.CompletedTask;
    }

    /// <summary>Minimal <see cref="ISettingsManager"/> double: only <see cref="GetObjectSettingAsync"/> is used
    /// (by the dictionaries endpoint, to read the configured languages); the rest are inert.</summary>
    private sealed class TestSettingsManager : ISettingsManager
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
                // Non-null AllowedValues everywhere: FileExtensionService unions the white/blacklist settings'
                // AllowedValues without a null guard.
                AllowedValues = name == PlatformConstants.Settings.General.Languages.Name ? ["en-US", "de-DE"] : [],
            };
            return Task.FromResult(setting);
        }

        public Task<IEnumerable<ObjectSettingEntry>> GetObjectSettingsAsync(IEnumerable<string> names, string objectType = null, string objectId = null)
            => Task.FromResult<IEnumerable<ObjectSettingEntry>>([]);

        public Task SaveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => Task.CompletedTask;
        public Task RemoveObjectSettingsAsync(IEnumerable<ObjectSettingEntry> objectSettings) => Task.CompletedTask;
    }

    /// <summary>
    /// In-memory <see cref="IStoreService"/> double shared by two features: (1) <c>SalesRepService</c> reads a
    /// store's <c>Customer.ContactDefaultStatus</c> setting to seed a rep's member status — present only when a
    /// test configured it for that store id via <see cref="ContactDefaultStatusByStore"/>; and (2) the VCST-5309
    /// statistics service reads the store's default currency — every store reports <c>EUR</c> (≠ the USD primary)
    /// so tests can prove the resolver used the store default and not the primary fallback. Returns a store for any
    /// id with a non-null (possibly empty) settings collection, mirroring the real service. Only <c>GetAsync</c>
    /// (which backs the <c>GetNoCloneAsync</c> extension the services call) is meaningful; the rest are inert.
    /// </summary>
    internal sealed class TestStoreService : IStoreService
    {
        public ConcurrentDictionary<string, string> ContactDefaultStatusByStore { get; } = new();

        // Sender From address per store (drives the email channel's store scoping + EmailUnavailable checks).
        public ConcurrentDictionary<string, string> EmailByStore { get; } = new();

        // Trusted groups per store (mirrors Store.TrustedGroups for the store-access check).
        public ConcurrentDictionary<string, IList<string>> TrustedGroupsByStore { get; } = new();

        public Task<IList<Store>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
        {
            // Return a store for EVERY id (the VCST-5309 statistics tests need every store to report its default
            // currency + test catalog), carrying both features' fields: EUR/Catalog (statistics) and Email/
            // TrustedGroups (the email channel's sender + store-access check), plus ContactDefaultStatus when a test
            // configured it.
            var stores = (ids ?? [])
                .Select(id =>
                {
                    var store = new Store
                    {
                        Id = id,
                        DefaultCurrency = "EUR",
                        Catalog = SalesRepTestContext.TestCatalogId, // the catalog the Top Sellers category filter reads
                        Email = EmailByStore.GetValueOrDefault(id),
                        TrustedGroups = TrustedGroupsByStore.GetValueOrDefault(id) ?? [],
                        Settings = [],
                    };

                    if (ContactDefaultStatusByStore.TryGetValue(id, out var status))
                    {
                        store.Settings =
                        [
                            new ObjectSettingEntry
                            {
                                Name = CustomerSettings.ContactDefaultStatus.Name,
                                ValueType = SettingValueType.ShortText,
                                Value = status,
                            },
                        ];
                    }

                    return store;
                })
                .ToList();
            return Task.FromResult<IList<Store>>(stores);
        }

        public Task<IList<Store>> GetByOuterIdsAsync(IList<string> outerIds, string responseGroup = null, bool clone = true)
            => Task.FromResult<IList<Store>>([]);

        public Task SaveChangesAsync(IList<Store> models) => Task.CompletedTask;

        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => Task.CompletedTask;

        public Task<IList<string>> GetUserAllowedStoreIdsAsync(ApplicationUser user)
            => Task.FromResult<IList<string>>([]);
    }
}
