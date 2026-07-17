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

        // Field-selection → member response group for the customer + rep-contact queries (load addresses/phones/
        // emails only when asked).
        serviceCollection.AddSingleton<ISalesRepMemberResponseGroupParser, SalesRepMemberResponseGroupParser>();

        // Currency defaulting (requested → store default → platform primary) shared by every dashboard query, so a
        // store's widgets all fold and display money in one currency. A project overrides it (last registration wins).
        serviceCollection.AddTransient<ISalesRepCurrencyResolver, SalesRepCurrencyResolver>();

        // Order statuses (filter options) + status→underlying mapping + raw-status localization. Default = each
        // Order.Status value 1:1; a project registers its own after this to hide/add/compose (last registration wins).
        serviceCollection.AddTransient<ISalesRepOrderFilterRuleResolver, SalesRepOrderFilterRuleResolver>();

        // Cart kinds (filter options) + kind→underlying type/status mapping for the cart/project statistics widgets.
        // Default = a single built-in "project" kind (cart type "Wishlist"); a project registers its own after this
        // to hide/add/recompose kinds (last registration wins).
        serviceCollection.AddTransient<ISalesRepCartFilterRuleResolver, SalesRepCartFilterRuleResolver>();

        // Customer segments (filter options) shared by the customers list + "my customers" counts. Default = a single
        // "All" segment (baseline, all served customers); a project registers its own after this to add real segments
        // (last registration wins).
        serviceCollection.AddTransient<ISalesRepCustomerFilterRuleResolver, SalesRepCustomerFilterRuleResolver>();

        // Orderings (sort options) for the orders list + customers list — a separate axis from the filter rules
        // above (filters choose which records, sorts choose their order). Defaults: orders = "recent"; customers =
        // "my last orders" / "ytd purchases" / "name". A project registers its own after this (last registration wins).
        serviceCollection.AddTransient<ISalesRepOrderSortRuleResolver, SalesRepOrderSortRuleResolver>();
        serviceCollection.AddTransient<ISalesRepCustomerSortRuleResolver, SalesRepCustomerSortRuleResolver>();

        // Top Sellers rules: orderings (by-units default, by-revenue) and category badges (the store catalog's
        // top-level non-hidden categories, expanded to a subtree on selection). A project registers its own after
        // this (last registration wins).
        serviceCollection.AddTransient<ISalesRepTopSellerSortRuleResolver, SalesRepTopSellerSortRuleResolver>();
        serviceCollection.AddTransient<ISalesRepTopSellerFilterRuleResolver, SalesRepTopSellerFilterRuleResolver>();

        return serviceCollection;
    }
}
