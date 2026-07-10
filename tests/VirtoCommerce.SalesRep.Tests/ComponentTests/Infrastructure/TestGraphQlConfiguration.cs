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
using VirtoCommerce.CoreModule.Core.Common;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.OrdersModule.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;

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

        // The REAL Orders search service — its BuildQuery applies the actual organization/store/prototype filters
        // and newest-first sort that the sales-rep code relies on. It hydrates via ICustomerOrderService.GetAsync,
        // supplied here by a repo-backed double (see below).
        services.AddTransient<ICustomerOrderService, RepositoryBackedCustomerOrderService>();
        services.AddTransient<ICustomerOrderSearchService, CustomerOrderSearchService>();

        // The sales-rep service under test — now goes through the public ICustomerOrderSearchService.
        services.AddTransient<ISalesRepCustomerOrderSearchService, SalesRepCustomerOrderSearchService>();

        // Sales statistics service under test (VCST-5309): the REAL CustomerOrderStatisticsService aggregating
        // over the same order repository. Its peripheral dependencies — the currency data source and the store
        // lookup — are stood in by fixed test doubles (USD primary; EUR at 1.25; every store's default = EUR), so
        // the conversion/fold math is deterministic and asserted directly.
        services.AddSingleton<ILogger<CustomerOrderStatisticsService>>(NullLogger<CustomerOrderStatisticsService>.Instance);
        services.AddSingleton<ICurrencyService, TestCurrencyService>();
        services.AddSingleton<IStoreService, TestStoreService>();
        services.AddTransient<ICustomerOrderStatisticsService, CustomerOrderStatisticsService>();

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

        services.AddGraphQL(builder =>
        {
            builder.AddSchema(services, typeof(XapiAssemblyMarker)); // graph types + MediatR handlers + ISchemaBuilders
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
    /// Fixed currency source for the statistics tests: USD primary (rate 1) and EUR at 1.25 — i.e. 1 EUR = 1.25 USD,
    /// using the same "rate relative to primary" convention as the real Currency table. Stands in for the (peripheral)
    /// currency data source so conversions are deterministic.
    /// </summary>
    private sealed class TestCurrencyService : ICurrencyService
    {
        public Task<IEnumerable<Currency>> GetAllCurrenciesAsync()
        {
            IEnumerable<Currency> currencies =
            [
                new Currency(Language.InvariantLanguage, "USD", "US Dollar", "$", 1m) { IsPrimary = true },
                new Currency(Language.InvariantLanguage, "EUR", "Euro", "€", 1.25m),
            ];
            return Task.FromResult(currencies);
        }

        public Task SaveChangesAsync(Currency[] currencies) => throw new NotSupportedException();

        public Task DeleteCurrenciesAsync(string[] codes) => throw new NotSupportedException();
    }

    /// <summary>
    /// Store lookup double: every store reports EUR as its default currency, so the "default currency" path
    /// (omit currencyCode, pass a store) resolves to EUR — distinct from the USD primary, which proves the resolver
    /// used the store default and not the primary fallback.
    /// </summary>
    private sealed class TestStoreService : IStoreService
    {
        public Task<IList<Store>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
        {
            IList<Store> stores = ids.Select(id => new Store { Id = id, DefaultCurrency = "EUR" }).ToList();
            return Task.FromResult(stores);
        }

        public Task<IList<Store>> GetByOuterIdsAsync(IList<string> outerIds, string responseGroup = null, bool clone = true) => throw new NotSupportedException();

        public Task SaveChangesAsync(IList<Store> models) => throw new NotSupportedException();

        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => throw new NotSupportedException();

        public Task<IList<string>> GetUserAllowedStoreIdsAsync(ApplicationUser user) => throw new NotSupportedException();
    }
}
