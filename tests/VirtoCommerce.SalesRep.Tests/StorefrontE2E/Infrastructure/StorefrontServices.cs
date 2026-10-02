using GraphQL;
using GraphQL.Types;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.CustomerModule.Data.OpenIddict;
using VirtoCommerce.CustomerModule.Data.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Security;
using VirtoCommerce.Platform.Security.OpenIddict;
using VirtoCommerce.ProfileExperienceApiModule.Data.Aggregates;
using VirtoCommerce.ProfileExperienceApiModule.Data.Aggregates.Contact;
using VirtoCommerce.ProfileExperienceApiModule.Data.Aggregates.Organization;
using VirtoCommerce.ProfileExperienceApiModule.Data.Schemas;
using VirtoCommerce.ProfileExperienceApiModule.Data.Services;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models;
using VirtoCommerce.Xapi.Core.Queries;
using VirtoCommerce.Xapi.Core.Services;
using VirtoCommerce.Xapi.Core.Subscriptions;
using VirtoCommerce.Xapi.Data.Queries;
using VirtoCommerce.Xapi.Data.Services;
using VirtoCommerce.XCart.Core.Services;
using SalesRepModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;
using ProfileUserType = VirtoCommerce.ProfileExperienceApiModule.Data.Schemas.UserType;
using StoreModuleConstants = VirtoCommerce.StoreModule.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// What the real storefront needs from the backend beyond the sales-rep schema, split by where it is registered:
/// harness overrides (services and schema), web-host services (authentication, GraphQL transport) and the pipeline.
/// Real code wherever a referenced package has it; the rest are the smallest stand-ins that satisfy the storefront.
/// </summary>
internal static class StorefrontServices
{
    public const string DefaultCulture = "en-US";

    /// <summary>Applied as the harness's last-wins overrides, so the component tests keep their own defaults.</summary>
    public static void AddStorefrontShell(IServiceCollection services)
    {
        // Identity claim types as the platform configures them (OpenIddict names), so principals built by the REAL
        // CustomUserClaimsPrincipalFactory carry sub/name/role/email exactly as production tokens do.
        services.Configure<IdentityOptions>(options =>
        {
            options.ClaimsIdentity.UserIdClaimType = OpenIddictConstants.Claims.Subject;
            options.ClaimsIdentity.UserNameClaimType = OpenIddictConstants.Claims.Name;
            options.ClaimsIdentity.RoleClaimType = OpenIddictConstants.Claims.Role;
            options.ClaimsIdentity.EmailClaimType = OpenIddictConstants.Claims.Email;
        });

        // The REAL claim producers (platform + customer module): system role, memberId, permissions, organization_id
        // and the organization-scoped permissions, exactly as /connect/token assembles them.
        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, CustomUserClaimsPrincipalFactory>();
        services.AddTransient<ITokenClaimProvider, OrganizationIdClaimProvider>();
        services.AddSingleton<TestTokenService>();

        // store(domain): the REAL Xapi query builder + handler over the harness store double, plus small stand-ins
        // for the services the handler reads around the store.
        services.Configure<StoresOptions>(options => options.DefaultStore = InProcessBackend.StoreId);
        services.Configure<GraphQLWebSocketOptions>(_ => { });
        services.AddTransient<IStoreDomainResolverService, StoreDomainResolverService>();
        services.AddSingleton<IStoreSearchService, EmptyStoreSearchService>();
        services.AddSingleton<IStoreCurrencyResolver, CurrencyServiceStoreCurrencyResolver>();
        services.AddSingleton<IStoreAuthenticationService, PasswordOnlyStoreAuthenticationService>();
        services.AddSingleton<IModuleService, StorefrontModuleService>();
        services.AddSingleton<ISettingsManager, StorefrontSettingsManager>();
        services.AddSingleton<IAppManifestService, NullAppManifestService>();
        services.AddSingleton<ISchemaBuilder, GetStoreQueryBuilder>();
        services.AddTransient<IRequestHandler<GetStoreQuery, StoreResponse>, GetStoreQueryHandler>();

        // pageContext.user: the REAL profile graph types (UserType -> ContactType -> Organization) over the harness's
        // real member and membership services, and the real user query handler.
        // Every profile handler (lazy, resolved per request as the module registers them): the user query the page
        // context sends, and the organization/contact lookups the profile graph types send while resolving.
        services.AddMediatR(configuration => configuration.RegisterServicesFromAssembly(typeof(ProfileUserType).Assembly));
        services.AddSingleton<IMemberAggregateFactory, MemberAggregateFactory>();
        services.AddTransient<IMemberAggregateRootRepository, MemberAggregateRootRepository>();
        services.AddTransient<IContactAggregateRepository, ContactAggregateRepository>();
        services.AddTransient<IOrganizationAggregateRepository, OrganizationAggregateRepository>();
        services.AddTransient<IAddressService, AddressService>();
        services.AddTransient<IAddressSearchService, AddressSearchService>();
        services.AddTransient<IFavoriteAddressService, FavoriteAddressService>();
        services.AddTransient<IMemberAddressService, MemberAddressService>();

        // Constructor dependencies of the real cart graph types (cart resolves to null, but the type must build).
        services.AddSingleton<ICartAvailMethodsService, NoCartAvailMethodsService>();

        // The shell's remaining root fields: schema-complete (real response types, so the storefront's selection sets
        // validate) and data-empty.
        services.AddSingleton<ISchemaBuilder, StorefrontShellSchemaBuilder>();
        services.AddTransient<PageContextResponseType>();
        services.AddTransient<MenuLinkListType>();
        services.AddTransient<MenuLinkType>();
        services.AddTransient<SearchHistoryResultType>();

        services.AddGraphQL(builder => builder.AddGraphTypes(typeof(ProfileUserType).Assembly));
        services.AddTransient<OrganizationType, StorefrontOrganizationType>();

        // One schema over every registered builder (sales-rep + store + shell): the storefront sends ALL operations
        // to /graphql; the scoped /graphql/sales-rep endpoint only serves codegen.
        services.AddSingleton<ISchema, SchemaFactory>();
    }

    /// <summary>Web-host-only registrations: bearer authentication for the storefront's token, GraphQL user context.</summary>
    public static void AddWebServices(IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = TestTokenService.Issuer,
                    ValidAudience = TestTokenService.Audience,
                    IssuerSigningKey = TestTokenService.SigningKey,
                    NameClaimType = OpenIddictConstants.Claims.Name,
                    RoleClaimType = OpenIddictConstants.Claims.Role,
                };
            });

        // AddIdentity (copied in with the harness) made the cookie scheme the default; bearer is the storefront's way in.
        services.PostConfigure<AuthenticationOptions>(options =>
        {
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        services.AddDataProtection().UseEphemeralDataProtectionProvider();

        // What Xapi.Web's Module does for the HTTP transport: the GraphQL user context is the request principal.
        // Exception details stay on: a failing resolver then names its cause in the test output.
        services.AddGraphQL(builder => builder
            .AddUserContextBuilder(httpContext => new GraphQLUserContext(httpContext.User))
            .AddErrorInfoProvider(options => options.ExposeExceptionDetails = true));
    }

    public static void ConfigurePipeline(WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();

        // Xapi.Core's own HTTP transport registration; the Xapi module calls the same extension for /graphql.
        app.UseSchemaGraphQL<ISchema>(schemaIntrospectionEnabled: false);

        app.MapGet("/health", () => Results.Text("Healthy"));
        TestTokenEndpoints.Map(app);
    }

    /// <summary>The store the harness double hands out, enriched with what the storefront boot reads from it.</summary>
    public static void ConfigureStoreDouble(SalesRepTestContext context)
    {
        context.GetRequiredService<TestServicesConfiguration.TestStoreService>().Customize = store =>
        {
            store.Name = "E2E store";
            store.Languages = [DefaultCulture];
            store.DefaultLanguage = DefaultCulture;
            store.Currencies = ["USD", "EUR"];

            // Public module settings reach the storefront as store.settings.modules; SalesRep.Enabled gates the hub.
            store.Settings.Add(new ObjectSettingEntry
            {
                ModuleId = "VirtoCommerce.SalesRep",
                Name = SalesRepModuleConstants.Settings.General.SalesRepEnabled.Name,
                ValueType = SettingValueType.Boolean,
                Value = true,
                IsPublic = true,
            });
            store.Settings.Add(new ObjectSettingEntry
            {
                Name = StoreModuleConstants.Settings.General.AllowAnonymousUsers.Name,
                ValueType = SettingValueType.Boolean,
                Value = true,
            });
        };
    }
}
