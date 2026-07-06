using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Introspection;
using GraphQL.MicrosoftDI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Adds the order data slice (real OrderDbContext/repository on SQLite + the sales-rep order-search subclass)
/// and the real Sales Rep X-API GraphQL stack, so component tests can execute actual GraphQL query strings
/// through the real schema, MediatR handlers and services. No mocks — the only stand-in is a no-op
/// <see cref="ICustomerOrderService"/> that merely satisfies the base search-service constructor (the tested
/// "latest order per organization" path goes straight to the order repository).
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

        // Only satisfies the base CustomerOrderSearchService constructor; never invoked by the tested path.
        services.AddSingleton<ICustomerOrderService, NoOpCustomerOrderService>();

        // The real subclass under test — its GetLatestOrdersByOrganizationIdsAsync uses the order repository.
        services.AddTransient<ISalesRepCustomerOrderSearchService, SalesRepCustomerOrderSearchService>();

        return services;
    }

    public static IServiceCollection AddSalesRepGraphQl(this IServiceCollection services)
    {
        // IAuthorizationService is required by the query builders' base constructor.
        services.AddAuthorization();

        // ScopedSchemaFactory depends on ISchemaFilter (registered by Xapi.Data in production).
        services.AddSingleton<ISchemaFilter, DefaultSchemaFilter>();

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
    /// Minimal <see cref="ICustomerOrderService"/> used only to construct the base search service.
    /// </summary>
    private sealed class NoOpCustomerOrderService : ICustomerOrderService
    {
        public Task<IList<CustomerOrder>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
            => Task.FromResult<IList<CustomerOrder>>([]);

        public Task<IList<CustomerOrder>> GetByOuterIdsAsync(IList<string> outerIds, string responseGroup = null, bool clone = true)
            => Task.FromResult<IList<CustomerOrder>>([]);

        public Task SaveChangesAsync(IList<CustomerOrder> models) => Task.CompletedTask;

        public Task DeleteAsync(IList<string> ids, bool softDelete = false) => Task.CompletedTask;
    }
}
