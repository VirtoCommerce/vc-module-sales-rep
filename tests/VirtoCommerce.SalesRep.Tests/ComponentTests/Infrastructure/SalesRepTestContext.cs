using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.CustomerModule.Data.Handlers;
using VirtoCommerce.CustomerModule.Data.Repositories;
using VirtoCommerce.CustomerModule.Data.Search;
using VirtoCommerce.CustomerModule.Data.Search.Indexing;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Events;
using VirtoCommerce.Platform.Security.Repositories;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.SalesRep.Tests.Infrastructure;
using VirtoCommerce.SalesRep.Web.Controllers.Api;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Isolated per-test harness: real SalesRep + platform-security + customer + order services over in-memory
/// SQLite databases, wired exactly as the modules wire them (registrations ported from their Module.Initialize).
/// Tests act through the real <see cref="SalesRepController"/> (REST) and the real Sales Rep GraphQL schema
/// (X-API) and assert against the databases / results.
/// </summary>
internal sealed class SalesRepTestContext : IDisposable
{
    private readonly SqliteConnection _securityConnection;
    private readonly SqliteConnection _customerConnection;
    private readonly SqliteConnection _orderConnection;
    private readonly ServiceProvider _provider;
    private readonly DbContextOptions<SecurityDbContext> _securityOptions;
    private readonly DbContextOptions<CustomerDbContext> _customerOptions;
    private readonly DbContextOptions<OrderDbContext> _orderOptions;

    private SalesRepTestContext(
        SqliteConnection securityConnection,
        SqliteConnection customerConnection,
        SqliteConnection orderConnection,
        ServiceProvider provider,
        DbContextOptions<SecurityDbContext> securityOptions,
        DbContextOptions<CustomerDbContext> customerOptions,
        DbContextOptions<OrderDbContext> orderOptions)
    {
        _securityConnection = securityConnection;
        _customerConnection = customerConnection;
        _orderConnection = orderConnection;
        _provider = provider;
        _securityOptions = securityOptions;
        _customerOptions = customerOptions;
        _orderOptions = orderOptions;
    }

    public static SalesRepTestContext Create()
    {
        // The platform resolves the current user id from these claim types; they are configured at platform
        // startup, so set them here for the GraphQL current-user resolution to work in tests.
        ClaimsPrincipalExtensions.UserIdClaimTypes = [ClaimTypes.NameIdentifier];

        var securityConnection = SqliteTestDbContextFactory.CreateConnection();
        var customerConnection = SqliteTestDbContextFactory.CreateConnection();
        var orderConnection = SqliteTestDbContextFactory.CreateConnection();
        var securityOptions = SqliteTestDbContextFactory.CreateOptions<SecurityDbContext>(securityConnection);
        var customerOptions = SqliteTestDbContextFactory.CreateOptions<CustomerDbContext>(customerConnection);
        var orderOptions = SqliteTestDbContextFactory.CreateOptions<OrderDbContext>(orderConnection);

        var provider = new ServiceCollection()
            .AddSecuritySlice(securityOptions)
            .AddCustomerSlice(customerOptions)
            .AddSalesRepSlice()
            .AddOrderSlice(orderOptions)
            .AddSalesRepGraphQl()
            .BuildServiceProvider();

        // Subscribe the customer delete-cascade handler to the in-process bus — mirrors the customer module's
        // appBuilder.RegisterEventHandler<UserChangedEvent, DeleteOrganizationMembershipUserChangedEventHandler>().
        provider.GetRequiredService<IEventHandlerRegistrar>()
            .RegisterEventHandler<UserChangedEvent>(provider.GetRequiredService<DeleteOrganizationMembershipUserChangedEventHandler>());

        // Register the Member search-request builder (done in the customer module's PostInitialize) so keyword
        // member searches — which route to the index and resolve a builder by document type — work in tests.
        provider.GetRequiredService<ISearchRequestBuilderRegistrar>()
            .Register(KnownDocumentTypes.Member, provider.GetRequiredService<MemberSearchRequestBuilder>);

        return new SalesRepTestContext(
            securityConnection, customerConnection, orderConnection,
            provider, securityOptions, customerOptions, orderOptions);
    }

    /// <summary>The real REST controller resolved from DI (the REST tests' entry point).</summary>
    public SalesRepController Controller => _provider.GetRequiredService<SalesRepController>();

    public T GetRequiredService<T>() where T : notnull => _provider.GetRequiredService<T>();

    /// <summary>
    /// Seed real Organization members (via the real IMemberService) so reps can be assigned to them —
    /// a rep's served orgs must be existing organizations (the profile's Organizations become MemberRelations
    /// whose FK references the organization member).
    /// </summary>
    public async Task SeedOrganizationsAsync(params string[] organizationIds)
    {
        var memberService = _provider.GetRequiredService<IMemberService>();
        var orgs = organizationIds
            .Select(id =>
            {
                var org = AbstractTypeFactory<Organization>.TryCreateInstance();
                org.Id = id;
                org.Name = id;
                return (Member)org;
            })
            .ToArray();
        await memberService.SaveChangesAsync(orgs);
    }

    /// <summary>
    /// Populate the (in-memory Lucene) member search index for the given member ids using the real member
    /// document builder, so keyword member searches — which route to the index, not the DB — return results.
    /// </summary>
    public async Task IndexMembersAsync(params string[] memberIds)
    {
        var documentBuilder = (IIndexDocumentBuilder)_provider.GetRequiredService<MemberDocumentBuilder>();
        var documents = await documentBuilder.GetDocumentsAsync(memberIds, CancellationToken.None);
        var searchProvider = _provider.GetRequiredService<ISearchProvider>();
        await searchProvider.IndexAsync(KnownDocumentTypes.Member, documents);
    }

    /// <summary>
    /// Execute a GraphQL query string against the real Sales Rep scoped schema as an authenticated caller.
    /// Returns the serialized GraphQL response (data + errors) for assertions.
    /// </summary>
    public Task<string> ExecuteGraphQlAsync(string query, string userId = null, string organizationId = null)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test"); // non-null type => IsAuthenticated
        if (!string.IsNullOrEmpty(userId))
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        if (!string.IsNullOrEmpty(organizationId))
        {
            identity.AddClaim(new Claim("organization_id", organizationId));
        }

        return ExecuteGraphQlInternalAsync(query, new ClaimsPrincipal(identity));
    }

    /// <summary>Execute a GraphQL query string as an anonymous (unauthenticated) caller.</summary>
    public Task<string> ExecuteGraphQlAnonymousAsync(string query)
    {
        return ExecuteGraphQlInternalAsync(query, new ClaimsPrincipal(new ClaimsIdentity()));
    }

    private async Task<string> ExecuteGraphQlInternalAsync(string query, ClaimsPrincipal principal)
    {
        var executer = _provider.GetRequiredService<IDocumentExecuter>();
        var schema = _provider.GetRequiredService<ScopedSchemaFactory<XapiAssemblyMarker>>();
        var serializer = _provider.GetRequiredService<IGraphQLTextSerializer>();

        var result = await executer.ExecuteAsync(options =>
        {
            options.Schema = schema;
            options.Query = query;
            options.RequestServices = _provider;
            options.UserContext = new GraphQLUserContext(principal);
        });

        return serializer.Serialize(result);
    }

    /// <summary>Fresh DbContext on the customer DB for assertions (avoids tracking conflicts).</summary>
    public CustomerDbContext NewCustomerDbContext() => new(_customerOptions);

    /// <summary>Fresh DbContext on the security DB for assertions.</summary>
    public SecurityDbContext NewSecurityDbContext() => new(_securityOptions);

    /// <summary>Fresh DbContext on the order DB for seeding/assertions.</summary>
    public OrderDbContext NewOrderDbContext() => new(_orderOptions);

    /// <summary>Unwraps the value from a controller action result (actions return <c>Ok(value)</c>).</summary>
    public static T Unwrap<T>(ActionResult<T> result)
    {
        return result.Result is OkObjectResult ok ? (T)ok.Value : result.Value;
    }

    public void Dispose()
    {
        _provider.Dispose();
        _securityConnection.Dispose();
        _customerConnection.Dispose();
        _orderConnection.Dispose();
    }
}
