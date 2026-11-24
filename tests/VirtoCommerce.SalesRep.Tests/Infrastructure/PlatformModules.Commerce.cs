using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Ganss.Xss;
using GraphQL.MicrosoftDI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.AssetsModule.Core.Services;
using VirtoCommerce.AssetsModule.Data.Repositories;
using VirtoCommerce.AssetsModule.Data.Services;
using VirtoCommerce.CartModule.Core.Model;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.CartModule.Data.Repositories;
using VirtoCommerce.CartModule.Data.Services;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.OutlinePart;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Search.Barcodes;
using VirtoCommerce.CatalogModule.Core.Search.Indexed;
using VirtoCommerce.CatalogModule.Core.Search.Sorting;
using VirtoCommerce.CatalogModule.Core.Search.Sorting.Resolvers;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CatalogModule.Data.Repositories;
using VirtoCommerce.CatalogModule.Data.Search;
using VirtoCommerce.CatalogModule.Data.Search.Barcodes;
using VirtoCommerce.CatalogModule.Data.Search.BrowseFilters;
using VirtoCommerce.CatalogModule.Data.Search.Indexing;
using VirtoCommerce.CatalogModule.Data.Search.Sorting;
using VirtoCommerce.CatalogModule.Data.Services;
using VirtoCommerce.CatalogModule.Data.Validation;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.CoreModule.Core.Package;
using VirtoCommerce.CoreModule.Data.Currency;
using VirtoCommerce.CoreModule.Data.Package;
using VirtoCommerce.CoreModule.Data.Repositories;
using VirtoCommerce.CoreModule.Data.Services;
using VirtoCommerce.CustomerModule.Core.Events;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.CustomerModule.Core.Services.Indexed;
using VirtoCommerce.CustomerModule.Data.Handlers;
using VirtoCommerce.CustomerModule.Data.OpenIddict;
using VirtoCommerce.CustomerModule.Data.Repositories;
using VirtoCommerce.CustomerModule.Data.Search;
using VirtoCommerce.CustomerModule.Data.Search.Indexing;
using VirtoCommerce.CustomerModule.Data.Services;
using VirtoCommerce.CustomerModule.Data.Validation;
using VirtoCommerce.FileExperienceApi.Core.Authorization;
using VirtoCommerce.FileExperienceApi.Core.Models;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.FileExperienceApi.Data.Authorization;
using VirtoCommerce.FileExperienceApi.Data.Services;
using VirtoCommerce.FileSystemAssetsModule.Core.Extensions;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.InventoryModule.Core.Services;
using VirtoCommerce.InventoryModule.Data.Repositories;
using VirtoCommerce.InventoryModule.Data.Search.Indexing;
using VirtoCommerce.InventoryModule.Data.Services;
using VirtoCommerce.MarketingModule.Core.Model;
using VirtoCommerce.MarketingModule.Core.Search;
using VirtoCommerce.MarketingModule.Core.Services;
using VirtoCommerce.MarketingModule.Data.Repositories;
using VirtoCommerce.MarketingModule.Data.Search;
using VirtoCommerce.MarketingModule.Data.Services;
using VirtoCommerce.PaymentModule.Core.Services;
using VirtoCommerce.PaymentModule.Data;
using VirtoCommerce.PaymentModule.Data.Repositories;
using VirtoCommerce.PaymentModule.Data.Services;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.DynamicProperties;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Events;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Security.OpenIddict;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.PricingModule.Core.Services;
using VirtoCommerce.PricingModule.Data.Common;
using VirtoCommerce.PricingModule.Data.Repositories;
using VirtoCommerce.PricingModule.Data.Search;
using VirtoCommerce.PricingModule.Data.Services;
using VirtoCommerce.PricingModule.Data.Validators;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.Seo.Core.Models;
using VirtoCommerce.Seo.Core.Services;
using VirtoCommerce.Seo.Data.Repositories;
using VirtoCommerce.Seo.Data.Services;
using VirtoCommerce.Seo.Data.Validation;
using VirtoCommerce.ShippingModule.Core.Search.Indexed;
using VirtoCommerce.ShippingModule.Core.Services;
using VirtoCommerce.ShippingModule.Data;
using VirtoCommerce.ShippingModule.Data.Repositories;
using VirtoCommerce.ShippingModule.Data.Search.Indexed;
using VirtoCommerce.ShippingModule.Data.Services;
using VirtoCommerce.StoreModule.Core;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.StoreModule.Data.Repositories;
using VirtoCommerce.StoreModule.Data.Services;
using VirtoCommerce.Xapi.Core.Extensions;
using CartPermissions = VirtoCommerce.CartModule.Core.ModuleConstants.Security.Permissions;
using CartSettings = VirtoCommerce.CartModule.Core.ModuleConstants.Settings;
using CatalogPermissions = VirtoCommerce.CatalogModule.Core.ModuleConstants.Security.Permissions;
using CatalogSettings = VirtoCommerce.CatalogModule.Core.ModuleConstants.Settings;
using CorePermissions = VirtoCommerce.CoreModule.Core.ModuleConstants.Security.Permissions;
using CoreSettings = VirtoCommerce.CoreModule.Core.ModuleConstants.Settings;
using CustomerSecurity = VirtoCommerce.CustomerModule.Core.ModuleConstants.Security;
using CustomerSettings = VirtoCommerce.CustomerModule.Core.ModuleConstants.Settings;
using FileExperienceApiMarker = VirtoCommerce.FileExperienceApi.Data.AssemblyMarker;
using FileExperienceApiPermissions = VirtoCommerce.FileExperienceApi.Core.ModuleConstants.Security.Permissions;
using InventoryPermissions = VirtoCommerce.InventoryModule.Core.ModuleConstants.Security.Permissions;
using InventorySettings = VirtoCommerce.InventoryModule.Core.ModuleConstants.Settings;
using MarketingPermissions = VirtoCommerce.MarketingModule.Core.ModuleConstants.Security.Permissions;
using MarketingSettings = VirtoCommerce.MarketingModule.Core.ModuleConstants.Settings;
using PaymentSettings = VirtoCommerce.PaymentModule.Core.ModuleConstants.Settings;
using PricingPermissions = VirtoCommerce.PricingModule.Core.ModuleConstants.Security.Permissions;
using PricingSettings = VirtoCommerce.PricingModule.Core.ModuleConstants.Settings;
using SeoSettings = VirtoCommerce.Seo.Core.ModuleConstants.Settings;
using ShippingPermissions = VirtoCommerce.ShippingModule.Core.ModuleConstants.Security.Permissions;
using ShippingSettings = VirtoCommerce.ShippingModule.Core.ModuleConstants.Settings;
using StorePermissions = VirtoCommerce.StoreModule.Core.ModuleConstants.Security.Permissions;
using StoreSettings = VirtoCommerce.StoreModule.Core.ModuleConstants.Settings;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

internal static partial class PlatformModules
{
    /// <summary>Stores, their settings and sign-in schemes (vc-module-store 3.1009.0).</summary>
    private static IServiceCollection AddStoreModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<StoreDbContext>();
        services.AddTransient<IStoreRepository, StoreRepository>();
        services.AddTransient<Func<IStoreRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetService<IStoreRepository>());
        services.AddTransient<IStoreService, StoreService>();
        services.AddTransient<IStoreSearchService, StoreSearchService>();
        services.AddTransient<ISeoResolver, StoreSeoResolver>();
        services.AddTransient<IStoreCurrencyResolver, StoreCurrencyResolver>();
        services.AddTransient<IPublicStoreSettings, PublicStoreSettings>();
        services.AddTransient<IStoreAuthenticationService, StoreAuthenticationService>();
        services.AddTransient<IStoreAuthenticationSchemeService, StoreAuthenticationSchemeService>();
        services.AddTransient<IStoreAuthenticationSchemeSearchService, StoreAuthenticationSchemeSearchService>();
        services.AddOptions<StoreAssetsOptions>();
        services.AddSingleton<IStoreAssetPublicUrlResolver, StoreAssetPublicUrlResolver>();

        return services;
    }

    /// <summary>
    /// The SEO module (vc-module-seo 3.1005.0), which X-API requires: the resolver over every module's SEO entries (X-API's
    /// slug), redirects and broken links.
    /// </summary>
    private static IServiceCollection AddSeoModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<SeoDbContext>();
        services.AddTransient<ISeoDuplicatesDetector, NullSeoDuplicateDetector>();
        services.AddTransient<ICompositeSeoResolver, CompositeSeoResolver>();
        services.AddTransient<IRedirectResolver, RedirectResolver>();
        services.AddTransient<IBrokenLinksRepository, BrokenLinksRepository>();
        services.AddSingleton<Func<IBrokenLinksRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IBrokenLinksRepository>());
        services.AddTransient<IRedirectRulesRepository, RedirectRulesRepository>();
        services.AddSingleton<Func<IRedirectRulesRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IRedirectRulesRepository>());
        services.AddTransient<IBrokenLinkSearchService, BrokenLinkSearchService>();
        services.AddTransient<IBrokenLinkService, BrokenLinkService>();
        services.AddTransient<IRedirectRuleSearchService, RedirectRuleSearchService>();
        services.AddTransient<IRedirectRuleService, RedirectRuleService>();
        services.AddTransient<ISeoExplainService, SeoExplainService>();
        services.AddTransient<AbstractValidator<RedirectRule>, RedirectRuleValidator>();

        return services;
    }

    /// <summary>Currencies, package types and number sequences (vc-module-core 3.1012.0).</summary>
    private static IServiceCollection AddCoreModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<CoreDbContext>();
        services.AddOptions<SequenceNumberGeneratorOptions>();
        services.AddTransient<ICoreRepository, CoreRepositoryImpl>();
        services.AddTransient<Func<ICoreRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<ICoreRepository>());
        services.AddTransient<ICurrencyService, CurrencyService>();
        services.AddTransient<IPackageTypesService, PackageTypesService>();
        services.AddTransient<IUniqueNumberGenerator, SequenceNumberGeneratorService>();
        services.AddTransient<ITenantUniqueNumberGenerator, SequenceNumberGeneratorService>();
        services.AddTransient<IMoneyRoundingPolicy, DefaultMoneyRoundingPolicy>();

        return services;
    }

    /// <summary>Contacts, employees and organizations, their memberships, addresses and preferences (vc-module-customer 3.1028.0).</summary>
    private static IServiceCollection AddCustomerModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<CustomerDbContext>();
        services.AddTransient<ICustomerRepository, CustomerRepository>();
        services.AddSingleton<Func<ICustomerRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<ICustomerRepository>());
        services.AddSingleton<Func<IMemberRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<ICustomerRepository>());
        services.AddTransient<IIndexedMemberSearchService, MemberIndexedSearchService>();
        services.AddTransient<IMemberSearchService, MemberSearchService>();
        services.AddTransient<IMemberService, MemberService>();
        services.AddTransient<IMemberResolver, MemberResolver>();
        services.AddTransient<MemberSearchRequestBuilder>();
        services.AddSingleton<IFavoriteAddressService, FavoriteAddressService>();
        services.AddSingleton<MemberDocumentChangesProvider>();
        services.AddSingleton<MemberDocumentBuilder>();
        services.AddSingleton(provider => new IndexDocumentConfiguration
        {
            DocumentType = KnownDocumentTypes.Member,
            DocumentSource = new IndexDocumentSource
            {
                ChangesProvider = provider.GetService<MemberDocumentChangesProvider>(),
                DocumentBuilder = provider.GetService<MemberDocumentBuilder>(),
            },
        });
        services.AddSingleton<AbstractValidator<Member>, MemberValidator>();
        services.AddSingleton<IOrganizationMembershipService, OrganizationMembershipService>();
        services.AddSingleton<IOrganizationMembershipSearchService, OrganizationMembershipSearchService>();
        services.AddSingleton<Func<IOrganizationMembershipSearchService>>(provider => () => provider.GetRequiredService<IOrganizationMembershipSearchService>());
        services.AddTransient<ICustomerPreferenceCrudService, CustomerPreferenceCrudService>();
        services.AddTransient<ICustomerPreferenceSearchService, CustomerPreferenceSearchService>();
        services.AddTransient<ICustomerPreferenceService, CustomerPreferenceService>();
        services.AddTransient<IInviteCustomerService, InviteCustomerService>();
        services.AddTransient<ICompanyMemberRoleService, CompanyMemberRoleService>();
        services.AddTransient<IAddressService, AddressService>();
        services.AddTransient<IAddressSearchService, AddressSearchService>();
        services.AddTransient<DeleteOrganizationMembershipUserChangedEventHandler>();
        // The organization a token carries (the token server's request validators and handlers go with it: not hosted).
        services.AddSingleton<ITokenClaimProvider, OrganizationIdClaimProvider>();

        return services;
    }

    /// <summary>Catalogs, categories, products, properties and their search (vc-module-catalog 3.1048.0).</summary>
    private static IServiceCollection AddCatalogModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<CatalogDbContext>();
        // The module ships its raw SQL for SQL Server, PostgreSQL and MySQL; this is its PostgreSQL SQL, run on SQLite.
        services.AddTransient<ICatalogRawDatabaseCommand, SqliteCatalogRawDatabaseCommand>();
        services.AddTransient<ICatalogRepository, CatalogRepositoryImpl>();
        services.AddTransient<Func<ICatalogRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<ICatalogRepository>());

        services.AddTransient<IProductSearchService, ProductSearchService>();
        services.AddTransient<ICategorySearchService, CategorySearchService>();
        services.AddTransient<ICatalogService, CatalogService>();
        services.AddTransient<ICatalogSearchService, CatalogSearchService>();
        services.AddTransient<ICategoryService, CategoryService>();
        services.AddTransient<ICategoryProductResolver, CategoryProductResolver>();
        services.AddTransient<ICategoryIndexedSearchService, CategoryIndexedSearchService>();
        services.AddTransient<IItemService, ItemService>();
        services.AddTransient<IProductIndexedSearchService, ProductIndexedSearchService>();
        services.AddTransient<IAssociationService, AssociationService>();
        services.AddTransient<IVideoSearchService, VideoSearchService>();
        services.AddTransient<IVideoService, VideoService>();
        services.AddTransient<IAggregationConverter, AggregationConverter>();
        services.AddTransient<IBrowseFilterService, BrowseFilterService>();
        services.AddTransient<ITermFilterBuilder, TermFilterBuilder>();
        services.AddTransient<IPropertyService, PropertyService>();
        services.AddTransient<IPropertySearchService, PropertySearchService>();
        services.AddTransient<IPropertyGroupService, PropertyGroupService>();
        services.AddTransient<IPropertyDictionaryItemService, PropertyDictionaryItemService>();
        services.AddTransient<IPropertyDictionaryItemSearchService, PropertyDictionaryItemSearchService>();
        services.AddTransient<IProductAssociationSearchService, ProductAssociationSearchService>();
        services.AddTransient<IOutlineService, OutlineService>();
        services.AddTransient<ISkuGenerator, DefaultSkuGenerator>();
        services.AddTransient<IMeasureService, MeasureService>();
        services.AddTransient<IMeasureSearchService, MeasureSearchService>();
        services.AddTransient<ISeoResolver, CatalogSeoResolver>();
        services.AddTransient<ISeoResolver, BrandSeoResolver>();
        services.AddTransient<CatalogSeoResolver>();

        services.AddSingleton<Func<PropertyValidationRule, PropertyValueValidator>>(rule => new PropertyValueValidator(rule));
        services.AddSingleton<AbstractValidator<IHasProperties>, HasPropertiesValidator>();
        services.AddSingleton<AbstractValidator<CatalogProduct>, ProductValidator>();
        services.AddSingleton<AbstractValidator<Property>, PropertyValidator>();

        services.AddTransient<IOutlinePartResolver, IdOutlinePartResolver>();
        services.AddTransient<IOutlinePartNameResolver, NameOutlinePartResolver>();

        services.AddTransient<ProductDocumentChangesProvider>();
        services.AddTransient<ProductDocumentBuilder>();
        services.AddTransient<CategoryDocumentChangesProvider>();
        services.AddTransient<CategoryDocumentBuilder>();
        services.AddSingleton(provider => new IndexDocumentConfiguration
        {
            DocumentType = KnownDocumentTypes.Product,
            DocumentSource = new IndexDocumentSource
            {
                ChangesProvider = provider.GetService<ProductDocumentChangesProvider>(),
                DocumentBuilder = provider.GetService<ProductDocumentBuilder>(),
            },
        });
        services.AddSingleton(provider => new IndexDocumentConfiguration
        {
            DocumentType = KnownDocumentTypes.Category,
            DocumentSource = new IndexDocumentSource
            {
                ChangesProvider = provider.GetService<CategoryDocumentChangesProvider>(),
                DocumentBuilder = provider.GetService<CategoryDocumentBuilder>(),
            },
        });
        services.AddTransient<ProductSearchRequestBuilder>();
        services.AddTransient<CategorySearchRequestBuilder>();

        services.AddTransient<ICategoryTreeService, CategoryTreeService>();
        services.AddTransient<IProductConfigurationService, ProductConfigurationService>();
        services.AddTransient<IProductConfigurationSearchService, ProductConfigurationSearchService>();
        services.AddSingleton<IHtmlSanitizer, HtmlSanitizer>();
        services.AddSingleton<IPropertyValueSanitizer, PropertyValueSanitizer>();
        services.AddTransient<IBrandSettingService, BrandSettingService>();
        services.AddSingleton<IProductSortingResolver, FeaturedProductSortingResolver>();
        services.AddSingleton<IProductSortingResolver, NameAscendingProductSortingResolver>();
        services.AddSingleton<IProductSortingResolver, NameDescendingProductSortingResolver>();
        services.AddSingleton<IProductSortingResolver, PriceAscendingProductSortingResolver>();
        services.AddSingleton<IProductSortingResolver, PriceDescendingProductSortingResolver>();
        services.AddSingleton<IProductSortingResolver, CreatedDateDescendingProductSortingResolver>();
        services.AddSingleton<IProductSortingResolver, CreatedDateAscendingProductSortingResolver>();
        services.AddSingleton<IProductSortingResolverRegistry, ProductSortingResolverRegistry>();
        services.AddTransient<IProductSortingService, ProductSortingService>();
        services.AddTransient<IProductSortableFieldService, ProductSortableFieldService>();
        services.AddTransient<IBarcodeSearchConfigurationService, BarcodeSearchConfigurationService>();
        services.AddSingleton<IProductSuggestionService, ProductSuggestionService>();

        return services;
    }

    /// <summary>Price lists, their assignments and the price evaluation (vc-module-pricing 3.1008.0): X-Catalog evaluates prices.</summary>
    private static IServiceCollection AddPricingModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<PricingDbContext>();
        services.AddTransient<IPricingRepository, PricingRepositoryImpl>();
        services.AddTransient<Func<IPricingRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IPricingRepository>());
        services.AddTransient<IPricingEvaluatorService, PricingEvaluatorService>();
        services.AddTransient<IPricelistAssignmentSearchService, PricelistAssignmentSearchService>();
        services.AddTransient<IPricelistSearchService, PricelistSearchService>();
        services.AddTransient<IPriceSearchService, PriceSearchService>();
        services.AddTransient<IPricelistAssignmentService, PricelistAssignmentService>();
        services.AddTransient<IPricelistService, PricelistService>();
        services.AddTransient<IPriceService, PriceService>();
        services.AddTransient<IPricingPriorityFilterPolicy, DefaultPricingPriorityFilterPolicy>();
        services.AddTransient<IPricingDocumentChangesProvider, ProductPriceDocumentChangesProvider>();
        services.AddTransient<ProductPriceDocumentBuilder>();
        services.AddTransient<ModuleConfigurator>();
        services.AddTransient<AbstractValidator<IEnumerable<PricelistAssignment>>, PricelistAssignmentsValidator>();
        services.AddTransient<AbstractValidator<Pricelist>, PriceListValidator>();

        return services;
    }

    /// <summary>
    /// Promotions and coupons (vc-module-marketing 3.1009.0). Optional to the module under test, but X-Cart requires it,
    /// and the module under test requires X-Cart: no deployment of this module lacks it.
    /// </summary>
    private static IServiceCollection AddMarketingModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<MarketingDbContext>();
        services.AddTransient<IMarketingRepository, MarketingRepository>();
        services.AddTransient<Func<IMarketingRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IMarketingRepository>());
        services.AddTransient<ICouponService, CouponService>();
        services.AddTransient<ICouponSearchService, CouponSearchService>();
        services.AddTransient<IPromotionService, PromotionService>();
        services.AddTransient<IPromotionSearchService, PromotionSearchService>();
        services.AddTransient<IPromotionUsageService, PromotionUsageService>();
        services.AddTransient<IPromotionUsageSearchService, PromotionUsageSearchService>();
        services.AddTransient<IPromotionRewardEvaluator, DefaultPromotionRewardEvaluator>();
        services.AddTransient<IMarketingPromoEvaluator>(provider =>
        {
            var settingsManager = provider.GetRequiredService<ISettingsManager>();
            var platformMemoryCache = provider.GetRequiredService<IPlatformMemoryCache>();
            var promotionService = provider.GetRequiredService<IPromotionSearchService>();
            var currencyService = provider.GetRequiredService<ICurrencyService>();

            if (settingsManager.GetValue<string>(MarketingSettings.General.CombinePolicy).EqualsIgnoreCase("CombineStackable"))
            {
                return new CombineStackablePromotionPolicy(currencyService, platformMemoryCache, promotionService, provider.GetRequiredService<IPromotionRewardEvaluator>());
            }

            return new BestRewardPromotionPolicy(currencyService, platformMemoryCache, promotionService);
        });

        return services;
    }

    /// <summary>
    /// Assets (vc-module-assets 3.1010.0) over the file system blob provider (vc-module-filesystem-assets 3.1006.0) in
    /// <paramref name="rootPath"/>, and the file experience API (3.1006.0): its upload service is the documents
    /// library's intake, as it is X-Cart's and X-Order's, with its upload scopes from FileUpload in the configuration.
    /// </summary>
    private static IServiceCollection AddAssetsModule(this IServiceCollection services, string rootPath, IConfiguration configuration)
    {
        services.AddModuleDatabase<AssetsDbContext>();
        services.AddTransient<IAssetsRepository, AssetsRepository>();
        services.AddSingleton<Func<IAssetsRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IAssetsRepository>());
        services.AddTransient<IAssetEntryService, AssetEntryService>();
        services.AddTransient<IAssetEntrySearchService, AssetEntrySearchService>();
        services.AddSingleton<IFileExtensionService, FileExtensionService>();

        services.AddFileSystemBlobProvider(options =>
        {
            options.RootPath = rootPath;
            options.PublicUrl = AssetsPublicUrl;
        });

        _ = new GraphQLBuilder(services, builder => builder.AddSchema(services, typeof(FileExperienceApiMarker)));
        services.AddOptions<FileUploadOptions>().Bind(configuration.GetSection("FileUpload")).ValidateDataAnnotations();
        services.AddSingleton<IFileUploadService, FileUploadService>();
        services.AddSingleton<IFileAuthorizationService, FileAuthorizationService>();
        services.AddSingleton<IAuthorizationHandler, FileAuthorizationHandler>();

        return services;
    }

    /// <summary>Stock (vc-module-inventory 3.1010.0): X-Cart loads a cart product's inventories, X-Catalog a product's.</summary>
    private static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<InventoryDbContext>();
        services.AddTransient<IInventoryRepository, InventoryRepositoryImpl>();
        services.AddTransient<Func<IInventoryRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IInventoryRepository>());
        services.AddTransient<IInventoryService, InventoryServiceImpl>();
        services.AddTransient<IInventorySearchService, InventorySearchService>();
        services.AddTransient<IFulfillmentCenterSearchService, FulfillmentCenterSearchService>();
        services.AddTransient<IFulfillmentCenterService, FulfillmentCenterService>();
        services.AddTransient<IProductInventorySearchService, ProductInventorySearchService>();
        services.AddTransient<IFulfillmentCenterGeoService, FulfillmentCenterGeoService>();
        services.AddTransient<IInventoryReservationService, InventoryReservationService>();
        services.AddTransient<ProductAvailabilityChangesProvider>();
        services.AddTransient<ProductAvailabilityDocumentBuilder>();

        return services;
    }

    /// <summary>Payment methods (vc-module-payment 3.1010.0): an order lists the methods its store offers.</summary>
    private static IServiceCollection AddPaymentModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<PaymentDbContext>();
        services.AddTransient<IPaymentRepository, PaymentRepository>();
        services.AddTransient<Func<IPaymentRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetService<IPaymentRepository>());
        services.AddTransient<IPaymentMethodsSearchService, PaymentMethodsSearchService>();
        services.AddTransient<IPaymentMethodsService, PaymentMethodsService>();
        services.AddTransient<IPaymentMethodsRegistrar, PaymentMethodsService>();

        return services;
    }

    /// <summary>Shipping methods and pickup locations (vc-module-shipping 3.1009.0): an order's shipment names its pickup location.</summary>
    private static IServiceCollection AddShippingModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<ShippingDbContext>();
        services.AddTransient<IShippingRepository, ShippingRepository>();
        services.AddTransient<Func<IShippingRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetService<IShippingRepository>());
        services.AddTransient<IPickupLocationService, PickupLocationService>();
        services.AddTransient<IPickupLocationSearchService, PickupLocationSearchService>();
        services.AddTransient<IShippingMethodsService, ShippingMethodsService>();
        services.AddTransient<IShippingMethodsRegistrar, ShippingMethodsService>();
        services.AddTransient<IShippingMethodsSearchService, ShippingMethodsSearchService>();
        services.AddTransient<IPickupLocationIndexedSearchService, PickupLocationIndexedSearchService>();
        services.AddTransient<PickupLocationSearchRequestBuilder>();
        services.AddSingleton<PickupLocationChangesProvider>();
        services.AddSingleton<PickupLocationDocumentBuilder>();

        return services;
    }

    /// <summary>Carts and wish lists (vc-module-cart 3.1012.0).</summary>
    private static IServiceCollection AddCartModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<CartDbContext>();
        // As the catalog's: the module's PostgreSQL SQL, run on SQLite.
        services.AddTransient<ICartRawDatabaseCommand, SqliteCartRawDatabaseCommand>();
        services.AddTransient<ICartRepository, CartRepository>();
        services.AddTransient<Func<ICartRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<ICartRepository>());
        services.AddTransient<IShoppingCartService, ShoppingCartService>();
        services.AddTransient<IShoppingCartSearchService, ShoppingCartSearchService>();
        services.AddTransient<IShoppingCartTotalsCalculator, DefaultShoppingCartTotalsCalculator>();
        services.AddTransient<IShoppingCartBuilder, ShoppingCartBuilder>();
        services.AddTransient<IWishlistService, WishlistService>();

        return services;
    }

    /// <summary>
    /// The commerce modules' PostInitialize: settings, permissions, dynamic property types, search request builders,
    /// payment and shipping methods, and the customer module's membership clean-up on account and member changes.
    /// </summary>
    private static void PostInitializeCommerceModules(IApplicationBuilder app)
    {
        var provider = app.ApplicationServices;
        var settings = provider.GetRequiredService<ISettingsRegistrar>();
        var permissions = provider.GetRequiredService<IPermissionsRegistrar>();
        var dynamicProperties = provider.GetRequiredService<IDynamicPropertyRegistrar>();
        var searchRequestBuilders = provider.GetRequiredService<ISearchRequestBuilderRegistrar>();

        dynamicProperties.RegisterType<Store>();
        settings.RegisterSettings(StoreSettings.AllSettings, "VirtoCommerce.Store");
        settings.RegisterSettingsForType(StoreSettings.AllSettings, nameof(Store));
        permissions.RegisterPermissions("VirtoCommerce.Store", "Store", StorePermissions.AllPermissions);

        settings.RegisterSettings(SeoSettings.AllSettings, "VirtoCommerce.Seo");

        settings.RegisterSettings(CoreSettings.AllSettings, "VirtoCommerce.Core");
        permissions.RegisterPermissions("VirtoCommerce.Core", "Core", CorePermissions.AllPermissions);

        settings.RegisterSettings(CustomerSettings.AllModuleSettings, "VirtoCommerce.Customer");
        settings.RegisterSettingsForType(CustomerSettings.StoreLevelSettings, nameof(Store));
        dynamicProperties.RegisterType<Organization>();
        dynamicProperties.RegisterType<Contact>();
        dynamicProperties.RegisterType<Vendor>();
        dynamicProperties.RegisterType<Employee>();
        permissions.RegisterPermissions("VirtoCommerce.Customer", "Customer", CustomerSecurity.Permissions.AllPermissions);
        permissions.RegisterPermissions("VirtoCommerce.Customer", "OrganizationMembership", CustomerSecurity.OrganizationMembershipPermissions.AllPermissions);
        app.RegisterEventHandler<UserChangedEvent, DeleteOrganizationMembershipUserChangedEventHandler>();
        app.RegisterEventHandler<MemberChangedEvent, DeleteOrganizationMembershipUserChangedEventHandler>();
        searchRequestBuilders.Register(KnownDocumentTypes.Member, provider.GetService<MemberSearchRequestBuilder>);

        settings.RegisterSettings(CatalogSettings.AllSettings, "VirtoCommerce.Catalog");
        settings.RegisterSettingsForType(CatalogSettings.StoreLevelSettings, nameof(Store));
        permissions.RegisterPermissions("VirtoCommerce.Catalog", "Catalog", CatalogPermissions.AllPermissions);
        searchRequestBuilders.Register(KnownDocumentTypes.Product, provider.GetService<ProductSearchRequestBuilder>);
        searchRequestBuilders.Register(KnownDocumentTypes.Category, provider.GetService<CategorySearchRequestBuilder>);

        settings.RegisterSettings(PricingSettings.AllSettings, "VirtoCommerce.Pricing");
        permissions.RegisterPermissions("VirtoCommerce.Pricing", "Pricing", PricingPermissions.AllPermissions);

        settings.RegisterSettings(MarketingSettings.General.AllSettings, "VirtoCommerce.Marketing");
        permissions.RegisterPermissions("VirtoCommerce.Marketing", "Marketing", MarketingPermissions.AllPermissions);
        dynamicProperties.RegisterType<DynamicContentItem>();

        permissions.RegisterPermissions("VirtoCommerce.FileExperienceApi", "FileExperienceApi", FileExperienceApiPermissions.AllPermissions);

        settings.RegisterSettings(InventorySettings.AllSettings, "VirtoCommerce.Inventory");
        permissions.RegisterPermissions("VirtoCommerce.Inventory", "Inventory", InventoryPermissions.AllPermissions);
        dynamicProperties.RegisterType<FulfillmentCenter>();

        settings.RegisterSettings(PaymentSettings.AllSettings, "VirtoCommerce.Payment");
        provider.GetRequiredService<IPaymentMethodsRegistrar>().RegisterPaymentMethod<DefaultManualPaymentMethod>();
        settings.RegisterSettingsForType(PaymentSettings.DefaultManualPaymentMethod.AllSettings, nameof(DefaultManualPaymentMethod));

        settings.RegisterSettings(ShippingSettings.AllSettings, "VirtoCommerce.Shipping");
        settings.RegisterSettingsForType(ShippingSettings.FixedRateShippingMethod.AllSettings, nameof(FixedRateShippingMethod));
        settings.RegisterSettingsForType(ShippingSettings.StoreSettings, nameof(Store));
        permissions.RegisterPermissions("VirtoCommerce.Shipping", "Shipping", ShippingPermissions.AllPermissions);
        var shippingMethods = provider.GetRequiredService<IShippingMethodsRegistrar>();
        shippingMethods.RegisterShippingMethod<FixedRateShippingMethod>();
        shippingMethods.RegisterShippingMethod<BuyOnlinePickupInStoreShippingMethod>();

        dynamicProperties.RegisterType<LineItem>();
        dynamicProperties.RegisterType<Payment>();
        dynamicProperties.RegisterType<Shipment>();
        dynamicProperties.RegisterType<ShoppingCart>();
        settings.RegisterSettings(CartSettings.General.AllSettings, "VirtoCommerce.Cart");
        settings.RegisterSettingsForType(CartSettings.StoreSettings, nameof(Store));
        permissions.RegisterPermissions("VirtoCommerce.Cart", "Cart", CartPermissions.AllPermissions);
    }

    /// <summary>
    /// Pricing and inventory add their prices and stock to the product index (a product's index document carries them):
    /// pricing through its <c>ModuleConfigurator</c>, inventory by adding its availability source.
    /// </summary>
    private static async Task PostInitializeSearchSourcesAsync(IServiceProvider provider)
    {
        await provider.GetRequiredService<ModuleConfigurator>().ConfigureSearchAsync();

        var productAvailability = new IndexDocumentSource
        {
            ChangesProvider = provider.GetRequiredService<ProductAvailabilityChangesProvider>(),
            DocumentBuilder = provider.GetRequiredService<ProductAvailabilityDocumentBuilder>(),
        };
        foreach (var configuration in provider.GetServices<IndexDocumentConfiguration>().Where(x => x.DocumentType == KnownDocumentTypes.Product))
        {
            configuration.RelatedSources ??= [];
            configuration.RelatedSources.Add(productAvailability);
        }
    }
}
