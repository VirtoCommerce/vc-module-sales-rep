using GraphQL.MicrosoftDI;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.ProfileExperienceApiModule.Data.Commands;
using VirtoCommerce.SalesRep.ExperienceApi.Queries;
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

        // Override the ProfileExperienceApi contacts search so an organization's contact roster
        // (storefront organization.contacts) omits the Sales Reps serving that org. The override is guarded to
        // org-scoped queries; the global Query.contacts is left untouched. This registration wins the DI
        // "last registration" over the built-in handler because the module depends on (loads after)
        // ProfileExperienceApiModule (see module.manifest).
        serviceCollection.AddTransient<IRequestHandler<SearchContactsQuery, MemberSearchResult>, SalesRepAwareSearchContactsQueryHandler>();

        return serviceCollection;
    }
}
