using GraphQL.MicrosoftDI;
using Microsoft.Extensions.DependencyInjection;
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

        return serviceCollection;
    }
}
