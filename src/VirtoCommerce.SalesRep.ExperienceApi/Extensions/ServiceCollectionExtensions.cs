using GraphQL.MicrosoftDI;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSalesRepExperienceApi(this IServiceCollection serviceCollection)
    {
        // Registers this assembly's graph types, MediatR handlers, AutoMapper profiles and ISchemaBuilders.
        _ = new GraphQLBuilder(serviceCollection, builder =>
        {
            builder.AddSchema(serviceCollection, typeof(XapiAssemblyMarker));
        });

        // Isolates these builders into their own scoped schema (exposed at /graphql/sales-rep by the Web module).
        serviceCollection.AddSingleton<ScopedSchemaFactory<XapiAssemblyMarker>>();

        // Field-selection → order response group (load only the order data the caller selected).
        serviceCollection.AddSingleton<ISalesRepOrderResponseGroupParser, SalesRepOrderResponseGroupParser>();

        // Field-selection → member response group for the customer queries (load addresses/phones only when asked).
        serviceCollection.AddSingleton<ISalesRepCustomerResponseGroupParser, SalesRepCustomerResponseGroupParser>();

        // Order statuses (filter options) + status→underlying mapping + raw-status localization. Default = each
        // Order.Status value 1:1; a project registers its own after this to hide/add/compose (last registration wins).
        serviceCollection.AddTransient<ISalesRepOrderStatusService, SalesRepOrderStatusService>();

        return serviceCollection;
    }
}
