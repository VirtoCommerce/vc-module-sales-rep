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
        _ = new GraphQLBuilder(serviceCollection, builder =>
        {
            builder.AddSchema(serviceCollection, typeof(XapiAssemblyMarker));
        });

        serviceCollection.AddSingleton<ScopedSchemaFactory<XapiAssemblyMarker>>();

        serviceCollection.AddSingleton<ISalesRepOrderResponseGroupParser, SalesRepOrderResponseGroupParser>();

        serviceCollection.AddSingleton<ISalesRepMemberResponseGroupParser, SalesRepMemberResponseGroupParser>();

        serviceCollection.AddSingleton<ISalesRepCommunicationResponseGroupParser, SalesRepCommunicationResponseGroupParser>();

        serviceCollection.AddTransient<ISalesRepOrderStatusService, SalesRepOrderStatusService>();

        return serviceCollection;
    }
}
