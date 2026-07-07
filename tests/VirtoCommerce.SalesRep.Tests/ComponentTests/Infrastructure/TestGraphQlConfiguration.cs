using System;
using GraphQL;
using GraphQL.Introspection;
using GraphQL.MicrosoftDI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Services;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Adds the order data slice (real OrderDbContext/repository on SQLite + the sales-rep latest-order lookup)
/// and the real Sales Rep X-API GraphQL stack, so component tests can execute actual GraphQL query strings
/// through the real schema, MediatR handlers and services. No mocks.
/// </summary>
internal static class TestGraphQlConfiguration
{
    public static IServiceCollection AddOrderSlice(this IServiceCollection services, DbContextOptions<OrderDbContext> orderDbOptions)
    {
        services.AddSingleton(orderDbOptions);
        services.AddScoped<OrderDbContext>();
        services.AddTransient<IOrderRepository, OrderRepository>();
        services.AddSingleton<Func<IOrderRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<IOrderRepository>());

        // The real service under test — its GetLatestOrdersByOrganizationIdsAsync uses the order repository.
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
}
