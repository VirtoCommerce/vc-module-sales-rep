using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Introspection;
using GraphQL.MicrosoftDI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using VirtoCommerce.CartModule.Data.Model;
using VirtoCommerce.CartModule.Data.Repositories;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Model.Search;
using VirtoCommerce.CatalogModule.Core.Search;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.CatalogModule.Data.Model;
using VirtoCommerce.CatalogModule.Data.Repositories;
using VirtoCommerce.CatalogModule.Data.Search;
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.Data.Services.Statistics;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Adds the order data slice (real OrderDbContext/repository on SQLite + the real Orders search service) and the
/// real Sales Rep X-API GraphQL stack, so component tests can execute actual GraphQL query strings through the
/// real schema, MediatR handlers and services. No mocks — the only stand-in is a repo-backed
/// <see cref="ICustomerOrderService"/> that hydrates orders (the real <c>CustomerOrderService</c> needs ~10
/// cross-module dependencies and is not the code under test).
/// </summary>
internal static class TestGraphQlConfiguration
{
    public static IServiceCollection AddOrderSlice(this IServiceCollection services, DbContextOptions<OrderDbContext> orderDbOptions)
    {
        services.AddSingleton(orderDbOptions);
        services.AddScoped<OrderDbContext>();
        services.AddTransient<IOrderRepository, OrderRepository>();
        services.AddSingleton<Func<IOrderRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<IOrderRepository>());
        services.Configure<CrudOptions>(_ => { });

        // The sales-rep order search under test IS the real Orders CustomerOrderSearchService (subclassed): its
        // inherited BuildQuery applies the real organization/store/prototype filters + newest-first sort, so both
        // the orders-list SearchAsync and the grouped "latest order per organization" query run against real
        // SQLite. Hydration goes through ICustomerOrderService.GetAsync, supplied by a repo-backed double (the real
        // CustomerOrderService needs ~10 cross-module deps and is not the code under test).
        services.AddTransient<ICustomerOrderService, RepositoryBackedCustomerOrderService>();
        services.AddTransient<ISalesRepCustomerOrderSearchService, SalesRepCustomerOrderSearchService>();

        // Sales statistics service under test (VCST-5309): the REAL CustomerOrderStatisticsService aggregating
        // over the same order repository. Its currency data source is a fixed double (USD primary; EUR at 1.25);
        // the store lookup is the shared TestServicesConfiguration.TestStoreService (every store's default = EUR),
        // so the conversion/fold math is deterministic and asserted directly.
        services.AddSingleton<ILogger<CustomerOrderStatisticsService>>(NullLogger<CustomerOrderStatisticsService>.Instance);
        services.AddSingleton<ICurrencyService, TestCurrencyService>();
        services.AddTransient<ICustomerOrderStatisticsService, CustomerOrderStatisticsService>();

        // "My customers" counts service (VCST dashboard): also aggregates over the same order repository.
        services.AddTransient<ISalesRepCustomerCountsService, SalesRepCustomerCountsService>();

        // "Top Sellers" ranking service (VCST-5309): the REAL service aggregating the rep's order line items over
        // the same order repository (units/revenue per product), so ranking/sort/period/category run end to end.
        services.AddSingleton<ILogger<SalesRepTopSellerService>>(NullLogger<SalesRepTopSellerService>.Instance);
        services.AddTransient<ISalesRepTopSellerService, SalesRepTopSellerService>();

        return services;
    }

    /// <summary>
    /// Adds the cart data slice (real CartDbContext/repository on SQLite) and the cart/project statistics stack, so
    /// component tests can execute the real <c>salesRepCustomerCartStatistics</c> query through the real schema and
    /// the real <see cref="CustomerCartStatisticsService"/>. The raw-database command (bulk soft-delete / wishlist
    /// lookup) is stubbed — the statistics path only reads the <c>ShoppingCarts</c> IQueryable.
    /// </summary>
    public static IServiceCollection AddCartSlice(this IServiceCollection services, DbContextOptions<CartDbContext> cartDbOptions)
    {
        services.AddSingleton(cartDbOptions);
        services.AddScoped<CartDbContext>();
        services.AddSingleton<ICartRawDatabaseCommand, StubCartRawDatabaseCommand>();
        services.AddTransient<ICartRepository, CartRepository>();
        services.AddSingleton<Func<ICartRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<ICartRepository>());

        // The real cart statistics service under test; currency source is the shared TestCurrencyService (USD+EUR).
        services.AddSingleton<ILogger<CustomerCartStatisticsService>>(NullLogger<CustomerCartStatisticsService>.Instance);
        services.AddTransient<ICustomerCartStatisticsService, CustomerCartStatisticsService>();

        // The real default cart-kind service (single built-in "project" kind → cart type "Wishlist").
        services.AddTransient<ISalesRepCartFilterRuleResolver, SalesRepCartFilterRuleResolver>();

        return services;
    }

    /// <summary>
    /// Adds the catalog data slice (real CatalogDbContext/repository on SQLite) and the REAL
    /// <see cref="CategorySearchService"/>, so the Top Sellers category filter (top-level categories + subtree
    /// expansion) runs through real code. Category hydration goes through a thin repo-backed
    /// <see cref="ICategoryService"/> double — the real CategoryService needs ~10 cross-module deps and is not the
    /// code under test, the same justified stand-in as the order-service double. The raw-database command is stubbed
    /// (the category search only reads the <c>Categories</c> IQueryable and hydrates by id).
    /// </summary>
    public static IServiceCollection AddCatalogSlice(this IServiceCollection services, DbContextOptions<CatalogDbContext> catalogDbOptions)
    {
        services.AddSingleton(catalogDbOptions);
        services.AddScoped<CatalogDbContext>();
        services.AddSingleton<ICatalogRawDatabaseCommand, StubCatalogRawDatabaseCommand>();
        services.AddTransient<ICatalogRepository, CatalogRepositoryImpl>();
        services.AddSingleton<Func<ICatalogRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<ICatalogRepository>());

        services.AddTransient<ICategoryService, RepositoryBackedCategoryService>();
        services.AddTransient<ICategorySearchService, CategorySearchService>();

        return services;
    }

    public static IServiceCollection AddSalesRepGraphQl(this IServiceCollection services)
    {
        // IAuthorizationService is required by the query builders' base constructor.
        services.AddAuthorization();

        // ScopedSchemaFactory depends on ISchemaFilter (registered by Xapi.Data in production).
        services.AddSingleton<ISchemaFilter, DefaultSchemaFilter>();

        // Field-selection → order response group, injected into the orders handler and lastOrder resolver.
        services.AddSingleton<ISalesRepOrderResponseGroupParser, SalesRepOrderResponseGroupParser>();

        // Field-selection → member response group, injected into the customer list/details + customerSalesReps handlers.
        services.AddSingleton<ISalesRepMemberResponseGroupParser, SalesRepMemberResponseGroupParser>();

        // Order statuses. A stub (not the real settings-backed default) stands in as a "project override" so the
        // tests exercise a composite status ("Inactive" → Cancelled + Failed) — proving the 1:many filter resolution
        // end to end. The real default SalesRepOrderFilterRuleResolver is unit-tested separately.
        services.AddSingleton<ISalesRepOrderFilterRuleResolver, StubOrderFilterRuleResolver>();

        // Customer segments: the real default resolver (single "All" baseline segment) — proves the shared seam's
        // passthrough (no filter / "All" → baseline) and fail-closed (any other segment name → no data) behavior on
        // the customers list + counts.
        services.AddSingleton<ISalesRepCustomerFilterRuleResolver, SalesRepCustomerFilterRuleResolver>();

        // Orderings (sort options): the real defaults (orders: "recent"; customers: my-last-orders / ytd / name),
        // so the discovery queries and the order-derived customer sort run end to end through real code.
        services.AddSingleton<ISalesRepOrderSortRuleResolver, SalesRepOrderSortRuleResolver>();
        services.AddSingleton<ISalesRepCustomerSortRuleResolver, SalesRepCustomerSortRuleResolver>();

        // Top Sellers: the real sort resolver (by-units/by-revenue). The category-badge resolver is a stub standing
        // in as a "project override" so component tests avoid a full catalog slice — it expands "electronics" to a
        // simulated subtree, exercising the subtree-restriction + fail-closed paths. The real catalog-backed resolver
        // is unit-tested separately.
        services.AddSingleton<ISalesRepTopSellerSortRuleResolver, SalesRepTopSellerSortRuleResolver>();
        // The REAL category-badge resolver — top-level categories from the (real) catalog slice, expanded to a
        // subtree on selection. Requires AddCatalogSlice.
        services.AddSingleton<ISalesRepTopSellerFilterRuleResolver, SalesRepTopSellerFilterRuleResolver>();

        // Localizable settings back the SalesRepOrderType.statusDisplayValue field (LocalizedField → TranslateAsync).
        // A stub renders a status as "<raw> (localized)" so the mapping is observable without real settings data.
        services.AddSingleton<ILocalizableSettingService, StubLocalizableSettingService>();

        // ICurrencyService (for MoneyType and the statistics conversion) is registered once in AddOrderSlice
        // (TestCurrencyService: USD primary + EUR, with a rounding policy). No second registration here — a second
        // AddSingleton would win by last-registration and shadow it, which previously broke the statistics tests.

        services.AddGraphQL(builder =>
        {
            builder.AddSchema(services, typeof(XapiAssemblyMarker)); // graph types + MediatR handlers + ISchemaBuilders
            builder.AddGraphTypes(typeof(MoneyType).Assembly);      // Xapi.Core graph types (MoneyType/CurrencyType) — SalesRepOrder.total is MoneyType
            builder.AddSystemTextJson();                            // IGraphQLTextSerializer for result assertions
            builder.AddDataLoader();                                // lastOrder batching
        });

        services.AddSingleton<ScopedSchemaFactory<XapiAssemblyMarker>>();

        return services;
    }

    /// <summary>
    /// Minimal <see cref="ICustomerOrderService"/> for the harness: hydrates orders straight from the order
    /// repository, passing the response group through so <c>WithPrices</c> still governs the grand total (the
    /// repository zeroes prices for lighter groups). Only the read path is exercised — by the real
    /// <see cref="CustomerOrderSearchService"/> under test; write/outer-id methods are not used.
    /// </summary>
    private sealed class RepositoryBackedCustomerOrderService : ICustomerOrderService
    {
        private readonly Func<IOrderRepository> _repositoryFactory;

        public RepositoryBackedCustomerOrderService(Func<IOrderRepository> repositoryFactory)
        {
            _repositoryFactory = repositoryFactory;
        }

        public async Task<IList<CustomerOrder>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
        {
            if (ids == null || ids.Count == 0)
            {
                return [];
            }

            using var repository = _repositoryFactory();
            var entities = await repository.GetCustomerOrdersByIdsAsync(ids.ToArray(), responseGroup);
            return entities.Select(x => x.ToModel(AbstractTypeFactory<CustomerOrder>.TryCreateInstance())).ToList();
        }

        public Task<IList<CustomerOrder>> GetByOuterIdsAsync(IList<string> outerIds, string responseGroup = null, bool clone = true)
            => throw new NotSupportedException();

        public Task SaveChangesAsync(IList<CustomerOrder> models) => throw new NotSupportedException();

        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();
    }

    /// <summary>
    /// The single fixed currency source for the whole harness: USD primary (rate 1) and EUR at 1.25 — i.e.
    /// 1 EUR = 1.25 USD, using the same "rate relative to primary" convention as the real Currency table. Serves
    /// both the statistics conversion/fold and the MoneyType resolvers (<c>SalesRepOrder.total</c> and the
    /// statistics money fields), so <c>RoundingPolicy</c> is set — <c>Money.Amount</c> calls it and would otherwise
    /// throw a NullReferenceException.
    /// </summary>
    private sealed class TestCurrencyService : ICurrencyService
    {
        public Task<IEnumerable<Currency>> GetAllCurrenciesAsync()
        {
            IEnumerable<Currency> currencies =
            [
                new Currency(Language.InvariantLanguage, "USD", "US Dollar", "$", 1m) { IsPrimary = true, RoundingPolicy = new DefaultMoneyRoundingPolicy() },
                new Currency(Language.InvariantLanguage, "EUR", "Euro", "€", 1.25m) { RoundingPolicy = new DefaultMoneyRoundingPolicy() },
            ];
            return Task.FromResult(currencies);
        }

        public Task SaveChangesAsync(Currency[] currencies) => throw new NotSupportedException();

        public Task DeleteCurrenciesAsync(string[] codes) => throw new NotSupportedException();
    }

    /// <summary>
    /// Stub cart raw-database command: the statistics path never touches it (it only reads the ShoppingCarts
    /// IQueryable), so the bulk soft-delete / wishlist-lookup methods throw if ever called.
    /// </summary>
    private sealed class StubCartRawDatabaseCommand : ICartRawDatabaseCommand
    {
        public Task SoftRemove(CartDbContext dbContext, IList<string> ids) => throw new NotSupportedException();

        public Task<IList<ProductWishlistEntity>> FindWishlistsByProductsAsync(CartDbContext dbContext, string customerId, string organizationId, string storeId, IList<string> productIds)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Stand-in status service acting as a "project override": a 1:1 rule ("New") plus a composite ("Inactive" →
    /// Cancelled + Failed) so tests can prove the rule list and the 1:many (composite) filter resolution.
    /// </summary>
    private sealed class StubOrderFilterRuleResolver : ISalesRepOrderFilterRuleResolver
    {
        private static readonly IList<SalesRepOrderFilterRule> _statuses =
        [
            SalesRepOrderFilterRule.Create("New", "New", "New"),
            SalesRepOrderFilterRule.Create("Inactive", "Not active", "Cancelled", "Failed"),
        ];

        public Task<IList<SalesRepOrderFilterRule>> GetRulesAsync(string storeId, string cultureName)
            => Task.FromResult(_statuses);

        public Task<CustomerOrderSearchCriteria> ApplyListFilterAsync(string storeId, string filter, CustomerOrderSearchCriteria criteria)
        {
            var resolved = Resolve(filter);
            if (resolved == null)
            {
                return Task.FromResult<CustomerOrderSearchCriteria>(null); // fail-closed
            }

            if (resolved.Length > 0)
            {
                criteria.Statuses = resolved;
            }

            return Task.FromResult(criteria);
        }

        public Task<CustomerOrderStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, string filter, CustomerOrderStatisticsCriteria criteria)
        {
            var resolved = Resolve(filter);
            if (resolved == null)
            {
                return Task.FromResult<CustomerOrderStatisticsCriteria>(null); // fail-closed
            }

            if (resolved.Length > 0)
            {
                criteria.Statuses = resolved;
            }

            return Task.FromResult(criteria);
        }

        // Shared resolution: empty = no filter; non-empty = the rule's statuses; null = a name was given but unknown.
        private static string[] Resolve(string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return [];
            }

            var rule = _statuses.FirstOrDefault(x => string.Equals(x.Name, filter, StringComparison.OrdinalIgnoreCase));
            return rule?.OrderStatuses;
        }
    }

    /// <summary>
    /// Stand-in localizable settings: renders a status as "&lt;raw&gt; (&lt;culture&gt;)" so LocalizedField's output is
    /// observable AND proves the culture reached the resolver. Mirrors the real service by returning the raw key
    /// unchanged when no culture is supplied.
    /// </summary>
    private sealed class StubLocalizableSettingService : ILocalizableSettingService
    {
        public Task<string> TranslateAsync(string key, string settingName, string languageCode)
            => Task.FromResult(string.IsNullOrEmpty(key) || string.IsNullOrEmpty(languageCode) ? key : $"{key} ({languageCode})");

        public Task<IList<KeyValue>> GetValuesAsync(string settingName, string languageCode)
            => Task.FromResult<IList<KeyValue>>([]);

        public Task<LocalizableSettingsAndLanguages> GetSettingsAndLanguagesAsync() => throw new NotSupportedException();
        public Task SaveAsync(string settingName, IList<DictionaryItem> items) => throw new NotSupportedException();
        public Task DeleteAsync(string settingName, IList<string> values) => throw new NotSupportedException();
    }

    /// <summary>
    /// Thin repo-backed <see cref="ICategoryService"/> for the harness: hydrates categories straight from the
    /// catalog repository (the real <c>CategoryService</c> needs ~10 cross-module deps and is not the code under
    /// test). Only the read path is exercised — by the real <see cref="CategorySearchService"/> under test; the
    /// write / code / outer-id methods are not used.
    /// </summary>
    private sealed class RepositoryBackedCategoryService : ICategoryService
    {
        private readonly Func<ICatalogRepository> _repositoryFactory;

        public RepositoryBackedCategoryService(Func<ICatalogRepository> repositoryFactory)
        {
            _repositoryFactory = repositoryFactory;
        }

        public async Task<IList<Category>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
        {
            if (ids == null || ids.Count == 0)
            {
                return [];
            }

            using var repository = _repositoryFactory();
            var entities = await repository.GetCategoriesByIdsAsync(ids.ToArray(), responseGroup);
            return entities.Select(x => x.ToModel(AbstractTypeFactory<Category>.TryCreateInstance())).ToList();
        }

        public Task<IList<Category>> GetByIdsAsync(IList<string> ids, string responseGroup, string catalogId)
            => GetAsync(ids, responseGroup);

        public Task<IDictionary<string, string>> GetIdsByCodes(string catalogId, IList<string> codes) => throw new NotSupportedException();
        public Task<IList<Category>> GetByOuterIdsAsync(IList<string> outerIds, string responseGroup = null, bool clone = true) => throw new NotSupportedException();
        public Task SaveChangesAsync(IList<Category> models) => throw new NotSupportedException();
        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();
    }

    /// <summary>
    /// Stub catalog raw-database command: the category search path never touches it (it only reads the Categories
    /// IQueryable and hydrates by id), so every method throws if ever called.
    /// </summary>
    private sealed class StubCatalogRawDatabaseCommand : ICatalogRawDatabaseCommand
    {
        public Task<IList<string>> GetAllSeoDuplicatesIdsAsync(CatalogDbContext dbContext) => throw new NotSupportedException();
        public Task<IList<CategoryHierarchyItem>> GetChildCategoriesAsync(CatalogDbContext dbContext, IList<string> categoryIds) => throw new NotSupportedException();
        public Task<GenericSearchResult<AssociationEntity>> SearchAssociations(CatalogDbContext dbContext, ProductAssociationSearchCriteria criteria) => throw new NotSupportedException();
        public Task<IList<CategoryEntity>> SearchCategoriesHierarchyAsync(CatalogDbContext dbContext, string categoryId) => throw new NotSupportedException();
        public Task RemoveItemsAsync(CatalogDbContext dbContext, IList<string> itemIds) => throw new NotSupportedException();
        public Task RemoveCategoriesAsync(CatalogDbContext dbContext, IList<string> ids) => throw new NotSupportedException();
        public Task RemoveCatalogsAsync(CatalogDbContext dbContext, IList<string> ids) => throw new NotSupportedException();
        public Task RemoveAllPropertyValuesAsync(CatalogDbContext dbContext, PropertyEntity catalogProperty, PropertyEntity categoryProperty, PropertyEntity itemProperty) => throw new NotSupportedException();
    }

}
