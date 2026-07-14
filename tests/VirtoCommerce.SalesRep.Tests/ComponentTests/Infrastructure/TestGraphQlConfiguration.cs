using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Introspection;
using GraphQL.MicrosoftDI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.OrdersModule.Data.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
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

        // Order statuses. A stub (not the real settings-backed default) stands in as a "project override" so the
        // tests exercise a composite status ("Inactive" → Cancelled + Failed) — proving the 1:many filter resolution
        // end to end. The real default SalesRepOrderStatusService is unit-tested separately.
        services.AddSingleton<ISalesRepOrderStatusService, StubOrderStatusService>();

        // Localizable settings back the SalesRepOrderType.statusDisplayValue field (LocalizedField → TranslateAsync).
        // A stub renders a status as "<raw> (localized)" so the mapping is observable without real settings data.
        services.AddSingleton<ILocalizableSettingService, StubLocalizableSettingService>();

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
    /// Stand-in status service acting as a "project override": a 1:1 status ("New") plus a composite ("Inactive" →
    /// Cancelled + Failed) so tests can prove the status list and the 1:many filter resolution (incl. multi-select
    /// union).
    /// </summary>
    private sealed class StubOrderStatusService : ISalesRepOrderStatusService
    {
        private static readonly IList<SalesRepOrderStatus> _statuses =
        [
            SalesRepOrderStatus.Create("New", "New", "New"),
            SalesRepOrderStatus.Create("Inactive", "Not active", "Cancelled", "Failed"),
        ];

        public Task<IList<SalesRepOrderStatus>> GetStatusesAsync(string storeId, string cultureName)
            => Task.FromResult(_statuses);

        public Task<string[]> ResolveOrderStatusesAsync(string storeId, IList<string> selectedStatusNames)
        {
            var selected = new HashSet<string>(selectedStatusNames ?? [], StringComparer.OrdinalIgnoreCase);
            var result = _statuses
                .Where(x => selected.Contains(x.Name))
                .SelectMany(x => x.OrderStatuses)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return Task.FromResult(result);
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
}
