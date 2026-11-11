using System;
using System.Security.Claims;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.MicrosoftDI;
using GraphQL.Server.Ui.GraphiQL;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Infrastructure.Validation;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Data.Extensions;
using VirtoCommerce.Xapi.Data.Security.Subscriptions;
using VirtoCommerce.XCart.Core.Services;
using VirtoCommerce.XCart.Data.Extensions;
using VirtoCommerce.XCatalog.Data.Extensions;
using VirtoCommerce.XOrder.Data.Extensions;
using ConfigKeys = VirtoCommerce.Xapi.Core.ModuleConstants.ConfigKeys;
using GraphQLWebSocketOptions = VirtoCommerce.Xapi.Core.Subscriptions.GraphQLWebSocketOptions;
using XapiCore = VirtoCommerce.Xapi.Core;
using XapiData = VirtoCommerce.Xapi.Data;
using XCartCore = VirtoCommerce.XCart.Core;
using XCartData = VirtoCommerce.XCart.Data;
using XCatalogCore = VirtoCommerce.XCatalog.Core;
using XCatalogData = VirtoCommerce.XCatalog.Data;
using XOrderCore = VirtoCommerce.XOrder.Core;
using XOrderData = VirtoCommerce.XOrder.Data;

namespace VirtoCommerce.SalesRep.Tests.Infrastructure;

internal static partial class PlatformModules
{
    /// <summary>
    /// X-API (vc-module-x-api 3.1027.0) as its module's Initialize registers it: the GraphQL server with its own
    /// schema (the store, slug, country and dynamic property queries), its Newtonsoft serializer, authorization,
    /// validation and user context, and its core services (<c>AddXCore</c>). Schema introspection is on, as the shipped
    /// appsettings.json sets GraphQLPlayground:Enable; X-API's own GraphiQL page ships in its module folder, so the
    /// playground serves GraphQL.Server's. GraphQL's naming switches are process-wide: <see cref="PlatformStatics"/>.
    /// </summary>
    private static IServiceCollection AddXapiModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddGraphQL(builder =>
        {
            builder
                .AddNewtonsoftJson()
                .AddSchema(services, typeof(XapiCore.CoreAssemblyMarker), typeof(XapiData.DataAssemblyMarker))
                .AddPermissionAuthorization()
                .ConfigureExecutionOptions(options => options.EnableMetrics = false)
                .AddErrorInfoProvider((options, serviceProvider) =>
                {
                    options.ExposeExtensions = true;

                    var environment = serviceProvider.GetService<IWebHostEnvironment>();
                    if (environment != null)
                    {
                        options.ExposeExceptionDetails = environment.IsDevelopment();
                    }
                })
                .AddUserContextBuilder(BuildUserContextAsync)
                .AddDataLoader()
                .AddValidationRule<ContentTypeValidationRule>()
                .AddWebSocketAuthentication<SubscriptionsUserContextResolver>();
        });
        services.AddTransient<Func<GraphiQLOptions>>(_ => () => new GraphiQLOptions());

        services.AddXCore(configuration);

        services.Configure<GraphQLPlaygroundOptions>(configuration.GetSection(ConfigKeys.GraphQlPlayground));
        services.Configure<GraphQLWebSocketOptions>(configuration.GetSection(ConfigKeys.GraphQlWebSocket));
        services.Configure<GraphQLComplexityValidationOptions>(configuration.GetSection(ConfigKeys.GraphQlComplexityValidation));
        services.Configure<StoresOptions>(configuration.GetSection(ConfigKeys.Stores));
        services.AddAuthenticationFilter(configuration);

        return services;
    }

    /// <summary>X-API's own user context builder (in its Web project, not on NuGet): the request principal, and the operator who signed in on behalf of a buyer.</summary>
    private static Task<GraphQLUserContext> BuildUserContextAsync(HttpContext context)
    {
        var userContext = new GraphQLUserContext(context.User);
        userContext.TrySetOperatorUserName(context.User.FindFirstValue(PlatformConstants.Security.Claims.OperatorUserName));

        return Task.FromResult(userContext);
    }

    /// <summary>X-Catalog (3.1023.0), as its module registers itself: every schema builder and handler of its assemblies, and its pipelines.</summary>
    private static IServiceCollection AddXCatalogModule(this IServiceCollection services)
    {
        var graphQlBuilder = new GraphQLBuilder(services, builder =>
        {
            builder.AddSchema(services, typeof(XCatalogCore.CoreAssemblyMarker), typeof(XCatalogData.DataAssemblyMarker));
        });
        services.AddXCatalog(graphQlBuilder);

        return services;
    }

    /// <summary>
    /// X-Cart (3.1039.0), as its module registers itself: the cart's queries and mutations, handlers, aggregate,
    /// validators and wish list sharing. Left out: its cart-changed handler.
    /// </summary>
    private static IServiceCollection AddXCartModule(this IServiceCollection services)
    {
        var graphQlBuilder = new GraphQLBuilder(services, builder =>
        {
            builder.AddSchema(services, typeof(XCartCore.CoreAssemblyMarker), typeof(XCartData.DataAssemblyMarker));
        });
        services.AddXCart(graphQlBuilder);

        return services;
    }

    /// <summary>X-Order (3.1011.0), as its module registers itself: the order types, queries and mutations and the order aggregate.</summary>
    private static IServiceCollection AddXOrderModule(this IServiceCollection services)
    {
        var graphQlBuilder = new GraphQLBuilder(services, builder =>
        {
            builder.AddSchema(services, typeof(XOrderCore.CoreAssemblyMarker), typeof(XOrderData.DataAssemblyMarker));
        });
        services.AddXOrder(graphQlBuilder);

        return services;
    }

    /// <summary>
    /// X-API's, X-Catalog's, X-Cart's and X-Order's PostInitialize: the GraphQL endpoints (<c>/graphql</c> and the
    /// modules' scoped ones), the filter that drops the identities GraphQL doesn't accept, and the settings.
    /// </summary>
    private static void PostInitializeExperienceApiModules(IApplicationBuilder app)
    {
        var provider = app.ApplicationServices;
        var settings = provider.GetRequiredService<ISettingsRegistrar>();

        app.UseAuthenticationFilter();
        app.UseWebSockets();
        app.UseSchemaGraphQL<GraphQL.Types.ISchema>(schemaIntrospectionEnabled: true);
        settings.RegisterSettings(XapiCore.ModuleConstants.Settings.General.AllSettings, "VirtoCommerce.Xapi");
        settings.RegisterSettingsForType(XapiCore.ModuleConstants.Settings.StoreLevelSettings, nameof(Store));

        app.UseScopedSchema<XCatalogData.DataAssemblyMarker>("catalog");

        app.UseScopedSchema<XCartData.DataAssemblyMarker>("cart");
        settings.RegisterSettings(XCartCore.ModuleConstants.Settings.General.AllSettings, "VirtoCommerce.XCart");
        settings.RegisterSettingsForType(XCartCore.ModuleConstants.Settings.StoreLevelSettings, nameof(Store));
        // As X-Cart does: a duplicate sharing scope policy fails the start, not the first wish list call.
        provider.GetRequiredService<ICartSharingService>();

        app.UseScopedSchema<XOrderData.DataAssemblyMarker>("order");
        settings.RegisterSettings(XOrderCore.ModuleConstants.Settings.General.AllSettings, "VirtoCommerce.XOrder");
        settings.RegisterSettingsForType(XOrderCore.ModuleConstants.Settings.StoreLevelSettings, nameof(Store));
    }
}
