using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using GraphQL.MicrosoftDI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.ContentModule.Core.Model;
using VirtoCommerce.ContentModule.Core.Search;
using VirtoCommerce.ContentModule.Core.Services;
using VirtoCommerce.ContentModule.Data.Repositories;
using VirtoCommerce.ContentModule.Data.Search;
using VirtoCommerce.ContentModule.Data.Services;
using VirtoCommerce.ContentModule.FileSystem;
using VirtoCommerce.ContentModule.FileSystem.Extensions;
using VirtoCommerce.MarketingModule.Core.Model.Promotions;
using VirtoCommerce.Platform.Core.DynamicProperties;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.PricingModule.Core.Model;
using VirtoCommerce.ProfileExperienceApiModule.Data.Aggregates;
using VirtoCommerce.ProfileExperienceApiModule.Data.Aggregates.Contact;
using VirtoCommerce.ProfileExperienceApiModule.Data.Aggregates.Organization;
using VirtoCommerce.ProfileExperienceApiModule.Data.Aggregates.Vendor;
using VirtoCommerce.ProfileExperienceApiModule.Data.Authorization;
using VirtoCommerce.ProfileExperienceApiModule.Data.Configuration;
using VirtoCommerce.ProfileExperienceApiModule.Data.Middlewares;
using VirtoCommerce.ProfileExperienceApiModule.Data.Schemas;
using VirtoCommerce.ProfileExperienceApiModule.Data.Services;
using VirtoCommerce.ProfileExperienceApiModule.Data.Validators;
using VirtoCommerce.SalesRep.Tests.Infrastructure;
using VirtoCommerce.Seo.Core.Services;
using VirtoCommerce.TaxModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Pipelines;
using VirtoCommerce.XFrontend.Core.Layouts.Commands;
using VirtoCommerce.XFrontend.Core.Layouts.Services;
using VirtoCommerce.XFrontend.Core.Statistics.Services;
using VirtoCommerce.XFrontend.Data.Layouts.Services;
using VirtoCommerce.XFrontend.Data.Layouts.Validation;
using VirtoCommerce.XFrontend.Data.Statistics.Services;
using VirtoCommerce.XRecommend.Core.Services;
using VirtoCommerce.XRecommend.Data.Authorization;
using VirtoCommerce.XRecommend.Data.Repositories;
using VirtoCommerce.XRecommend.Data.Services;
using ProfileMarker = VirtoCommerce.ProfileExperienceApiModule.Data.AssemblyMarker;
using Store = VirtoCommerce.StoreModule.Core.Model.Store;
using XCmsCore = VirtoCommerce.XCMS.Core;
using XCmsData = VirtoCommerce.XCMS.Data;
using XFrontendCore = VirtoCommerce.XFrontend.Core;
using XFrontendData = VirtoCommerce.XFrontend.Data;
using XRecommendCore = VirtoCommerce.XRecommend.Core;
using XRecommendData = VirtoCommerce.XRecommend.Data;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// The modules behind the storefront shell's own queries around the hub's pages, registered as their
/// <c>Module.Initialize</c> registers them (see <see cref="PlatformModules"/>): X-Profile (<c>me</c>, the organization
/// and the address book), X-Frontend (<c>pageContext</c>, over X-API's store and slug), X-CMS over the content
/// module's menus (<c>menu</c>), and X-Recommend (<c>searchHistory</c>).
/// </summary>
internal static class StorefrontShellModules
{
    private static readonly string _contentRootPath = Path.Combine(Path.GetTempPath(), "VirtoCommerce.SalesRep.Tests", "cms-content");

    public static IServiceCollection AddStorefrontShellModules(this IServiceCollection services)
    {
        return services
            .AddXProfileModule()
            .AddXFrontendModule()
            .AddContentModule()
            .AddXCmsModule()
            .AddXRecommendModule();
    }

    /// <summary>X-Profile (vc-module-profile-experience-api 3.1019.0, the newest on the X-Order 3.1011 this module references).</summary>
    private static IServiceCollection AddXProfileModule(this IServiceCollection services)
    {
        _ = new GraphQLBuilder(services, builder => builder.AddSchema(services, typeof(ProfileMarker)));
        services.AddSingleton<ScopedSchemaFactory<ProfileMarker>>();
        services.AddSingleton<IMemberAggregateFactory, MemberAggregateFactory>();
        services.AddSingleton<IProfileExperienceApiModuleMapper, ProfileExperienceApiModuleMapper>();
        services.AddOptions<InputValidationOptions>();
        services.AddTransient<NewContactValidator>();
        services.AddTransient<AccountValidator>();
        services.AddTransient<AddressValidator>();
        services.AddTransient<OrganizationValidator>();
        services.AddTransient<RegisterByInvitationCommandValidator>();
        services.AddTransient<PersonalDataValidator>();
        services.AddTransient<IMemberAggregateRootRepository, MemberAggregateRootRepository>();
        services.AddTransient<IOrganizationAggregateRepository, OrganizationAggregateRepository>();
        services.AddTransient<IContactAggregateRepository, ContactAggregateRepository>();
        services.AddTransient<IVendorAggregateRepository, VendorAggregateRepository>();
        services.AddTransient<IAccountService, AccountsService>();
        services.AddSingleton<IAuthorizationHandler, ProfileAuthorizationHandler>();
        services.AddSingleton<IProfileAuthorizationService, ProfileSchema>();
        services.AddSingleton<IMemberAddressService, MemberAddressService>();
        services.AddOptions<FrontendSecurityOptions>();
        services.AddPipeline<PromotionEvaluationContext>(builder => builder.AddMiddleware(typeof(LoadUserToEvalContextMiddleware)));
        services.AddPipeline<TaxEvaluationContext>(builder => builder.AddMiddleware(typeof(LoadUserToEvalContextMiddleware)));
        services.AddPipeline<PriceEvaluationContext>(builder => builder.AddMiddleware(typeof(LoadUserToEvalContextMiddleware)));
        services.AddPipeline<VendorAggregate>();

        return services;
    }

    /// <summary>
    /// X-Frontend (the VCST-6078 prerelease the csproj names): the page context, composed of X-Profile's user, X-API's store
    /// and slug, and the layout API the hub's dashboard and customer profile read and save theirs through. The services
    /// are the module's own registrations.
    /// </summary>
    private static IServiceCollection AddXFrontendModule(this IServiceCollection services)
    {
        services.AddTransient<IOrderStatisticsService, OrderStatisticsService>();
        services.AddTransient<IStatisticsCurrencyResolver, StatisticsCurrencyResolver>();
        services.AddTransient<ILayoutService, LayoutService>();
        services.AddTransient<AbstractValidator<SaveLayoutCommand>, SaveLayoutCommandValidator>();
        _ = new GraphQLBuilder(services, builder => builder.AddSchema(services, typeof(XFrontendCore.CoreAssemblyMarker), typeof(XFrontendData.DataAssemblyMarker)));

        return services;
    }

    /// <summary>
    /// The content module (vc-module-content 3.1008.0): its menus, which X-CMS reads, and its pages and files, over its
    /// file system provider, the default without Content:Provider (a temporary folder: nothing reads a page or a file).
    /// </summary>
    private static IServiceCollection AddContentModule(this IServiceCollection services)
    {
        services.AddModuleDatabase<MenuDbContext>();
        services.AddTransient<IMenuRepository, MenuRepository>();
        services.AddTransient<Func<IMenuRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetService<IMenuRepository>());
        services.AddTransient<IMenuLinkListService, MenuLinkListService>();
        services.AddTransient<IMenuLinkListSearchService, MenuLinkListSearchService>();
        services.AddTransient<IFullTextContentSearchService, FullTextContentSearchService>();
        services.AddTransient<IContentService, ContentService>();
        services.AddTransient<IContentStatisticService, ContentStatisticService>();
        services.AddTransient<IContentFileService, ContentFileService>();
        services.AddTransient<IPublishingService, PublishingServices>();
        services.AddTransient<IContentPathResolver, ContentPathResolver>();
        services.AddTransient<ISeoResolver, ContentSeoResolver>();
        services.AddTransient<ContentSeoResolver>();
        services.AddSingleton<IContentItemTypeRegistrar, ContentItemTypeRegistrar>();
        services.AddOptions<ContentOptions>();
        services.AddOptions<FileSystemContentBlobOptions>().Configure(options => options.RootPath = _contentRootPath);
        services.AddFileSystemContentBlobProvider();

        return services;
    }

    /// <summary>X-CMS (3.1009.0): menus and pages.</summary>
    private static IServiceCollection AddXCmsModule(this IServiceCollection services)
    {
        _ = new GraphQLBuilder(services, builder => builder.AddSchema(services, typeof(XCmsCore.CoreAssemblyMarker), typeof(XCmsData.DataAssemblyMarker)));

        return services;
    }

    /// <summary>X-Recommend (3.1002.0): recommendations, recently browsed products and the search history.</summary>
    private static IServiceCollection AddXRecommendModule(this IServiceCollection services)
    {
        _ = new GraphQLBuilder(services, builder => builder.AddSchema(services, typeof(XRecommendCore.CoreAssemblyMarker), typeof(XRecommendData.DataAssemblyMarker)));
        services.AddModuleDatabase<XRecommendDbContext>();
        services.AddTransient<IRecommendRepository, RecommendRepository>();
        services.AddTransient<Func<IRecommendRepository>>(provider => () => provider.CreateScope().ServiceProvider.GetRequiredService<IRecommendRepository>());
        services.AddTransient<IHistoricalEventService, HistoricalEventService>();
        services.AddTransient<IHistoricalEventSearchService, HistoricalEventSearchService>();
        services.AddTransient<IRecommendationsService, RelatedProductsRecommendationsService>();
        services.AddTransient<IRecommendationsService, BoughtTogetherRecommendationsService>();
        services.AddSingleton<IAuthorizationHandler, RecommendationsAuthorizationHandler>();
        services.AddTransient<ISearchQueryService, SearchQueryService>();
        services.AddTransient<ISearchQuerySearchService, SearchQuerySearchService>();
        services.AddSingleton<IAuthorizationHandler, SearchHistoryAuthorizationHandler>();

        return services;
    }

    /// <summary>These modules' PostInitialize: their endpoints, settings and the content module's dynamic properties.</summary>
    public static async Task PostInitializeAsync(IApplicationBuilder app)
    {
        var provider = app.ApplicationServices;
        var settings = provider.GetRequiredService<ISettingsRegistrar>();

        app.UseScopedSchema<ProfileMarker>("profile");

        settings.RegisterSettings(XFrontendCore.ModuleConstants.Settings.AllSettings, "VirtoCommerce.XFrontend");
        settings.RegisterSettingsForType(XFrontendCore.ModuleConstants.Settings.StoreLevelSettings, nameof(Store));

        provider.GetRequiredService<IDynamicPropertyRegistrar>().RegisterType<FrontMatterHeaders>();
        await provider.GetRequiredService<IDynamicPropertyService>().SaveDynamicPropertiesAsync(FrontMatterHeaders.AllDynamicProperties.ToArray());

        settings.RegisterSettings(XRecommendCore.ModuleConstants.Settings.AllSettings, "VirtoCommerce.XRecommend");
        settings.RegisterSettingsForType(XRecommendCore.ModuleConstants.Settings.StoreLevelSettings, nameof(Store));
    }
}
