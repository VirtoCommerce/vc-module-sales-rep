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

        serviceCollection.AddSingleton<ISalesRepCustomerOrderResponseGroupParser, SalesRepCustomerOrderResponseGroupParser>();

        serviceCollection.AddSingleton<ISalesRepMemberResponseGroupParser, SalesRepMemberResponseGroupParser>();

        serviceCollection.AddSingleton<ISalesRepCommunicationResponseGroupParser, SalesRepCommunicationResponseGroupParser>();

        serviceCollection.AddSingleton<ISalesRepCartStatisticsResponseGroupParser, SalesRepCartStatisticsResponseGroupParser>();

        serviceCollection.AddTransient<ISalesRepCurrencyResolver, SalesRepCurrencyResolver>();

        serviceCollection.AddTransient<ISalesRepOrderFilterRuleResolver, SalesRepOrderFilterRuleResolver>();

        serviceCollection.AddTransient<ISalesRepCartFilterRuleResolver, SalesRepCartFilterRuleResolver>();

        serviceCollection.AddTransient<ISalesRepCustomerFilterRuleResolver, SalesRepCustomerFilterRuleResolver>();

        serviceCollection.AddTransient<ISalesRepOrderSortRuleResolver, SalesRepOrderSortRuleResolver>();
        serviceCollection.AddTransient<ISalesRepCustomerSortRuleResolver, SalesRepCustomerSortRuleResolver>();

        serviceCollection.AddTransient<ISalesRepTopSellerSortRuleResolver, SalesRepTopSellerSortRuleResolver>();
        serviceCollection.AddTransient<ISalesRepTopSellerFilterRuleResolver, SalesRepTopSellerFilterRuleResolver>();

        serviceCollection.AddTransient<ISalesRepProductResolver, SalesRepProductResolver>();

        return serviceCollection;
    }
}
