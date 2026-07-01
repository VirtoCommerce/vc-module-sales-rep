using System;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.CustomerModule.Core.Services.Indexed;
using VirtoCommerce.CustomerModule.Data.Handlers;
using VirtoCommerce.CustomerModule.Data.Repositories;
using VirtoCommerce.CustomerModule.Data.Search;
using VirtoCommerce.CustomerModule.Data.Services;
using VirtoCommerce.CustomerModule.Data.Validation;
using VirtoCommerce.LuceneSearchModule.Data;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Bus;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Search;
using VirtoCommerce.Platform.Data.Common;
using VirtoCommerce.Platform.Security;
using VirtoCommerce.Platform.Security.Repositories;
using VirtoCommerce.Platform.Security.Services;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.SearchModule.Data.SearchPhraseParsing;
using VirtoCommerce.SearchModule.Data.Services;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Registers the REAL platform/customer/sales-rep services against in-memory SQLite for component tests
/// (ported from the modules' Module.Initialize/PostInitialize — the .Web assemblies aren't referenceable).
/// Built up incrementally; the security slice is validated first.
/// </summary>
internal static class TestServicesConfiguration
{
    /// <summary>
    /// Platform security on SQLite: real Identity with VC's custom stack (CustomUserStore/CustomUserManager/
    /// CustomRoleManager), real in-process event bus, and IUserSearchService — mirrors the platform Startup
    /// (AddIdentity + AddEntityFrameworkStores) followed by AddSecurityServices' custom overrides.
    /// </summary>
    public static IServiceCollection AddSecuritySlice(this IServiceCollection services, DbContextOptions<SecurityDbContext> securityDbOptions)
    {
        // Platform infra
        services.AddLogging();
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.Configure<CachingOptions>(_ => { });
        services.Configure<UserOptionsExtended>(_ => { });
        services.Configure<PasswordOptionsExtended>(_ => { });
        services.Configure<MvcNewtonsoftJsonOptions>(_ => { });
        services.AddSingleton<IPlatformMemoryCache, PlatformMemoryCache>();

        // Real in-process event bus (drives the customer delete-cascade handler later)
        services.AddSingleton<InProcessBus>();
        services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<InProcessBus>());
        services.AddSingleton<IEventHandlerRegistrar>(sp => sp.GetRequiredService<InProcessBus>());

        // Security DB (schema already materialized on the shared connection by the options factory)
        services.AddSingleton(securityDbOptions);
        services.AddScoped<SecurityDbContext>();
        services.AddScoped<ISecurityRepository, SecurityRepository>();
        services.AddSingleton<Func<ISecurityRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<ISecurityRepository>());

        // Identity
        services.AddIdentity<ApplicationUser, Role>(options => options.Stores.MaxLengthForKeys = 128)
            .AddEntityFrameworkStores<SecurityDbContext>()
            .AddDefaultTokenProviders();

        // VC custom overrides — explicit AddScoped so they win over AddIdentity's registrations
        services.AddScoped<IUserStore<ApplicationUser>, CustomUserStore>();
        services.AddScoped<RoleManager<Role>, CustomRoleManager>();
        services.AddScoped<UserManager<ApplicationUser>, CustomUserManager>();
        services.AddScoped<IdentityErrorDescriber, CustomIdentityErrorDescriber>();
        services.AddSingleton<IPermissionsRegistrar, DefaultPermissionProvider>();

        services.AddSingleton<Func<UserManager<ApplicationUser>>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
        services.AddSingleton<Func<RoleManager<Role>>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<RoleManager<Role>>());
        services.AddSingleton<IUserSearchService>(sp =>
        {
            var scope = sp.CreateScope();
            return new UserSearchService(
                scope.ServiceProvider.GetRequiredService<Func<UserManager<ApplicationUser>>>(),
                scope.ServiceProvider.GetRequiredService<Func<RoleManager<Role>>>());
        });

        return services;
    }

    /// <summary>
    /// Customer domain on SQLite: real Member and OrganizationMembership services + repositories + validator +
    /// countries, and the member indexed-search chain backed by an in-memory (RAM) Lucene provider (needed to
    /// construct MemberSearchService — keyword member searches route to the index, non-keyword to the DB).
    /// </summary>
    public static IServiceCollection AddCustomerSlice(this IServiceCollection services, DbContextOptions<CustomerDbContext> customerDbOptions)
    {
        services.Configure<PlatformOptions>(_ => { });

        // Customer DB (schema materialized by the options factory)
        services.AddSingleton(customerDbOptions);
        services.AddScoped<CustomerDbContext>();
        services.AddTransient<ICustomerRepository, CustomerRepository>();
        services.AddSingleton<Func<ICustomerRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<ICustomerRepository>());
        services.AddSingleton<Func<IMemberRepository>>(sp => () => sp.CreateScope().ServiceProvider.GetRequiredService<ICustomerRepository>());

        // Real countries service (Nager-backed, in memory)
        services.AddSingleton<FileSystemCountriesService>();
        services.AddSingleton<ICountriesService, CountriesService>();

        // Real member validator
        services.AddTransient<AbstractValidator<Member>, MemberValidator>();

        // Member indexed-search chain over an in-memory (RAM) Lucene provider
        services.AddTransient<ISearchPhraseParser, SearchPhraseParser>();
        services.AddSingleton<ISearchRequestBuilderRegistrar, SearchRequestBuilderRegistrar>();
        services.AddSingleton(Options.Create(new LuceneSearchOptions { UseInMemory = true }));
        services.AddSingleton(Options.Create(new SearchOptions { Provider = "Lucene", Scope = "test" }));
        services.AddSingleton<ISearchProvider, LuceneSearchProvider>();
        services.AddTransient<MemberSearchRequestBuilder>();
        services.AddTransient<IIndexedMemberSearchService, MemberIndexedSearchService>();

        // Real member + membership services
        services.AddTransient<IMemberService, MemberService>();
        services.AddTransient<IMemberSearchService, MemberSearchService>();
        services.AddSingleton<IOrganizationMembershipService, OrganizationMembershipService>();
        services.AddSingleton<IOrganizationMembershipSearchService, OrganizationMembershipSearchService>();
        services.AddSingleton<Func<IOrganizationMembershipSearchService>>(sp => () => sp.GetRequiredService<IOrganizationMembershipSearchService>());

        // Delete-cascade handler (subscribed to the in-process bus by the harness build step)
        services.AddTransient<DeleteOrganizationMembershipUserChangedEventHandler>();

        return services;
    }
}
