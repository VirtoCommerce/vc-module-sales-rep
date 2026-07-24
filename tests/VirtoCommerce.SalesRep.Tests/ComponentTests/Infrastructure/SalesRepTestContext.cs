using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CartModule.Data.Repositories;
using VirtoCommerce.CatalogModule.Data.Model;
using VirtoCommerce.CatalogModule.Data.Repositories;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.CustomerModule.Data.Handlers;
using VirtoCommerce.CustomerModule.Data.Repositories;
using VirtoCommerce.CustomerModule.Data.Search;
using VirtoCommerce.CustomerModule.Data.Search.Indexing;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Events;
using VirtoCommerce.Platform.Security.Caching;
using VirtoCommerce.Platform.Security.Repositories;
using VirtoCommerce.SalesRep.Core.Models;
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
    /// <summary>Catalog id every harness store reports; the seeded Top Sellers categories live under it.</summary>
    public const string TestCatalogId = "test-catalog";

    private readonly SqliteConnection _securityConnection;
    private readonly SqliteConnection _customerConnection;
    private readonly SqliteConnection _orderConnection;
    private readonly SqliteConnection _cartConnection;
    private readonly SqliteConnection _catalogConnection;
    private readonly ServiceProvider _provider;
    private readonly DbContextOptions<SecurityDbContext> _securityOptions;
    private readonly DbContextOptions<CustomerDbContext> _customerOptions;
    private readonly DbContextOptions<OrderDbContext> _orderOptions;
    private readonly DbContextOptions<CartDbContext> _cartOptions;
    private readonly DbContextOptions<CatalogDbContext> _catalogOptions;

    private SalesRepTestContext(
        SqliteConnection securityConnection,
        SqliteConnection customerConnection,
        SqliteConnection orderConnection,
        SqliteConnection cartConnection,
        SqliteConnection catalogConnection,
        ServiceProvider provider,
        DbContextOptions<SecurityDbContext> securityOptions,
        DbContextOptions<CustomerDbContext> customerOptions,
        DbContextOptions<OrderDbContext> orderOptions,
        DbContextOptions<CartDbContext> cartOptions,
        DbContextOptions<CatalogDbContext> catalogOptions)
    {
        _securityConnection = securityConnection;
        _customerConnection = customerConnection;
        _orderConnection = orderConnection;
        _cartConnection = cartConnection;
        _catalogConnection = catalogConnection;
        _provider = provider;
        _securityOptions = securityOptions;
        _customerOptions = customerOptions;
        _orderOptions = orderOptions;
        _cartOptions = cartOptions;
        _catalogOptions = catalogOptions;
    }

    /// <param name="configureOverrides">
    /// Optional last-wins registrations applied after the standard slices — a test uses it to shadow a default service
    /// with a project-override double (e.g. <see cref="OrderFilterRuleOverride.WithCompositeInactiveStatus"/> to
    /// exercise composite order-status resolution). Omit for the default (real-service) harness.
    /// </param>
    public static SalesRepTestContext Create(Action<IServiceCollection> configureOverrides = null)
    {
        // The platform resolves the current user id from these claim types; they are configured at platform
        // startup, so set them here for the GraphQL current-user resolution to work in tests.
        ClaimsPrincipalExtensions.UserIdClaimTypes = [ClaimTypes.NameIdentifier];

        var securityConnection = SqliteTestDbContextFactory.CreateConnection();
        var customerConnection = SqliteTestDbContextFactory.CreateConnection();
        var orderConnection = SqliteTestDbContextFactory.CreateConnection();
        var cartConnection = SqliteTestDbContextFactory.CreateConnection();
        var catalogConnection = SqliteTestDbContextFactory.CreateConnection();
        var securityOptions = SqliteTestDbContextFactory.CreateOptions<SecurityDbContext>(
            securityConnection,
            builder => builder.ReplaceService<IModelCustomizer, LockoutEndSqliteModelCustomizer>());
        var customerOptions = SqliteTestDbContextFactory.CreateOptions<CustomerDbContext>(customerConnection);
        var orderOptions = SqliteTestDbContextFactory.CreateOptions<OrderDbContext>(orderConnection);
        var cartOptions = SqliteTestDbContextFactory.CreateOptions<CartDbContext>(cartConnection);
        var catalogOptions = SqliteTestDbContextFactory.CreateOptions<CatalogDbContext>(catalogConnection);

        var services = new ServiceCollection()
            .AddSecuritySlice(securityOptions)
            .AddCustomerSlice(customerOptions)
            .AddSalesRepSlice()
            .AddOrderSlice(orderOptions)
            .AddCartSlice(cartOptions)
            .AddCatalogSlice(catalogOptions)
            .AddSalesRepGraphQl();

        // Per-test last-wins overrides (e.g. a composite order-status resolver), applied after the defaults.
        configureOverrides?.Invoke(services);

        var provider = services.BuildServiceProvider();

        // Subscribe the customer delete-cascade handler to the in-process bus — mirrors the customer module's
        // appBuilder.RegisterEventHandler<UserChangedEvent, DeleteOrganizationMembershipUserChangedEventHandler>().
        provider.GetRequiredService<IEventHandlerRegistrar>()
            .RegisterEventHandler<UserChangedEvent>(provider.GetRequiredService<DeleteOrganizationMembershipUserChangedEventHandler>());

        // Register the Member search-request builder (done in the customer module's PostInitialize) so keyword
        // member searches — which route to the index and resolve a builder by document type — work in tests.
        provider.GetRequiredService<ISearchRequestBuilderRegistrar>()
            .Register(KnownDocumentTypes.Member, provider.GetRequiredService<MemberSearchRequestBuilder>);

        return new SalesRepTestContext(
            securityConnection, customerConnection, orderConnection, cartConnection, catalogConnection,
            provider, securityOptions, customerOptions, orderOptions, cartOptions, catalogOptions);
    }

    /// <summary>The real REST controller resolved from DI (the REST tests' entry point).</summary>
    public SalesRepController Controller => _provider.GetRequiredService<SalesRepController>();

    public T GetRequiredService<T>() where T : notnull => _provider.GetRequiredService<T>();

    /// <summary>
    /// User id of the most recently created Sales Rep. <c>SeedOrder</c> defaults a seeded order's CustomerId to this,
    /// so orders count as "created by the rep" (rep-created orders record the rep's user id as CustomerId) without
    /// every test threading it through.
    /// </summary>
    public string LastCreatedRepUserId { get; private set; }

    /// <summary>
    /// Configure the <c>Customer.ContactDefaultStatus</c> setting the harness's <see cref="IStoreService"/> double
    /// reports for a store, so a rep created in that store inherits it as its member status (mirrors the real
    /// store setting, e.g. "Approved" for B2B-store).
    /// </summary>
    public void SetStoreContactDefaultStatus(string storeId, string status)
        => _provider.GetRequiredService<TestServicesConfiguration.TestStoreService>()
            .ContactDefaultStatusByStore[storeId] = status;

    /// <summary>Configure a store's sender From address (so the email channel resolves it and passes its store scoping).</summary>
    public void SetStoreEmail(string storeId, string email)
        => _provider.GetRequiredService<TestServicesConfiguration.TestStoreService>()
            .EmailByStore[storeId] = email;

    /// <summary>Configure a store's trusted groups (mirrors Store.TrustedGroups for the email store-access check).</summary>
    public void SetStoreTrustedGroups(string storeId, params string[] groups)
        => _provider.GetRequiredService<TestServicesConfiguration.TestStoreService>()
            .TrustedGroupsByStore[storeId] = [.. groups];

    /// <summary>
    /// Prime the platform's <see cref="UserManager{T}"/> memory cache for a user (as any read that goes
    /// through <c>FindByIdAsync</c> does in the running app). A subsequent edit then gets the cached instance,
    /// which comes from a foreign (already disposed) scope — the "warm cache" path of an account update.
    /// </summary>
    public async Task WarmUserCacheAsync(string userId)
    {
        using var userManager = _provider.GetRequiredService<Func<UserManager<ApplicationUser>>>()();
        await userManager.FindByIdAsync(userId);
    }

    /// <summary>
    /// Evict all security entries from the platform memory cache. The next account read is then a guaranteed
    /// cache miss, so <c>FindByIdAsync</c> loads the user through the calling manager's own DbContext — the
    /// "cold cache" path, where the returned instance is also EF-tracked by that context. That is the condition
    /// under which passing the instance back into <c>UpdateAsync</c> used to silently drop role changes.
    /// </summary>
    public static void ExpireSecurityCache()
    {
        SecurityCacheRegion.ExpireRegion();
    }

    /// <summary>
    /// Create an additional role that grants <c>sales-rep:access</c> (as an admin would when adding a custom
    /// Sales Rep role in the Security admin), so switching a rep between two granting roles can be exercised.
    /// </summary>
    public async Task<Role> CreateGrantingRoleAsync(string name)
    {
        using var roleManager = _provider.GetRequiredService<Func<RoleManager<Role>>>()();
        var role = AbstractTypeFactory<Role>.TryCreateInstance();
        role.Id = Guid.NewGuid().ToString("N");
        role.Name = name;
        role.Permissions = [new Permission { Name = SalesRep.Core.ModuleConstants.Security.Permissions.Access }];

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return role;
    }

    /// <summary>
    /// Create a role that does NOT grant <c>sales-rep:access</c> (e.g. a buyer/manager role an org member could
    /// hold), so behaviors that must preserve unrelated roles/memberships can be exercised.
    /// </summary>
    public async Task<Role> CreateNonGrantingRoleAsync(string name)
    {
        using var roleManager = _provider.GetRequiredService<Func<RoleManager<Role>>>()();
        var role = AbstractTypeFactory<Role>.TryCreateInstance();
        role.Id = Guid.NewGuid().ToString("N");
        role.Name = name;
        role.Permissions = [new Permission { Name = "customer:read" }];

        var result = await roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return role;
    }

    /// <summary>
    /// Create a bare login account linked to an existing contact member, with NO global roles — the state of a
    /// per-organization-only rep (whose sales-rep role is granted via memberships, not the account).
    /// </summary>
    public async Task<string> CreateAccountWithoutRolesAsync(string memberId, string email)
    {
        using var userManager = _provider.GetRequiredService<Func<UserManager<ApplicationUser>>>()();
        var user = AbstractTypeFactory<ApplicationUser>.TryCreateInstance();
        user.UserName = email;
        user.Email = email;
        user.MemberId = memberId;
        user.UserType = "Customer";

        var result = await userManager.CreateAsync(user, "P@ssw0rd123!");
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        return user.Id;
    }

    /// <summary>
    /// Add an organization membership carrying the given role for a user directly through the customer module's
    /// membership service (as an org-membership admin action outside the Sales Rep module would).
    /// </summary>
    public async Task<OrganizationMembership> AddMembershipAsync(string userId, string organizationId, Role role)
    {
        var membership = AbstractTypeFactory<OrganizationMembership>.TryCreateInstance();
        membership.UserId = userId;
        membership.OrganizationId = organizationId;
        var membershipRole = AbstractTypeFactory<OrganizationMembershipRole>.TryCreateInstance();
        membershipRole.RoleId = role.Id;
        membershipRole.RoleName = role.Name;
        membership.Roles = [membershipRole];

        await _provider.GetRequiredService<IOrganizationMembershipService>().SaveChangesAsync([membership]);
        return membership;
    }

    /// <summary>All organization memberships of a user, freshly loaded (for assertions and role edits).</summary>
    public async Task<IList<OrganizationMembership>> GetMembershipsAsync(string userId)
    {
        var criteria = AbstractTypeFactory<OrganizationMembershipSearchCriteria>.TryCreateInstance();
        criteria.UserId = userId;
        return await _provider.GetRequiredService<IOrganizationMembershipSearchService>().SearchAllAsync(criteria);
    }

    /// <summary>
    /// Backdate the rep's assignment date (the membership's creation date) for the given organization. The dashboard
    /// "new customers" counter counts organizations first assigned within a window, so a test controls the assignment
    /// date rather than relying on when <see cref="CreateRepAsync"/> happened to run. Uses a direct UPDATE so no audit
    /// interceptor re-stamps the date.
    /// </summary>
    public async Task SetMembershipAssignmentDateAsync(string userId, string organizationId, DateTime assignedDate)
    {
        using var db = NewCustomerDbContext();
        await db.Set<VirtoCommerce.CustomerModule.Data.Model.OrganizationMembershipEntity>()
            .Where(x => x.UserId == userId && x.OrganizationId == organizationId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CreatedDate, assignedDate));

        // The membership read path is cached (populated at creation): the search caches id-lists and the CRUD service
        // caches each hydrated model (with its creation date baked in). The raw UPDATE bypasses both, so expire both
        // regions or the handler keeps reading the stale assignment date.
        GenericSearchCachingRegion<OrganizationMembership>.ExpireRegion();
        GenericCachingRegion<OrganizationMembership>.ExpireRegion();
    }

    /// <summary>
    /// Create a Sales Rep (a login account + a contact serving the given organizations) through the real
    /// <see cref="SalesRepController"/>, and return the created details.
    /// </summary>
    public Task<SalesRepDetails> CreateRepAsync(string firstName, string lastName, string email, params string[] organizationIds)
        => CreateRepInStoreAsync(firstName, lastName, email, storeId: null, organizationIds);

    /// <summary>As <see cref="CreateRepAsync"/>, but binds the rep's account to a specific store.</summary>
    public async Task<SalesRepDetails> CreateRepInStoreAsync(string firstName, string lastName, string email, string storeId, params string[] organizationIds)
    {
        var rep = new SalesRepDetails
        {
            FirstName = firstName,
            LastName = lastName,
            Emails = [email],
            Phones = ["+1-555-0100"],
            Password = "P@ssw0rd123!",
            StoreId = storeId,
            Organizations = organizationIds.Select(id => new SalesRepOrganization { OrganizationId = id }).ToList(),
        };
        var created = Unwrap(await Controller.Create(rep));
        LastCreatedRepUserId = created.UserId;
        return created;
    }

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
    /// Seed a single Organization member, optionally configured (owner, business category, address, phone),
    /// so the richer <c>salesRepCustomer</c> detail fields can be exercised. Id and Name default to <paramref name="id"/>.
    /// </summary>
    public async Task<Organization> SeedOrganizationAsync(string id, Action<Organization> configure = null)
    {
        var org = AbstractTypeFactory<Organization>.TryCreateInstance();
        org.Id = id;
        org.Name = id;
        configure?.Invoke(org);
        await _provider.GetRequiredService<IMemberService>().SaveChangesAsync([org]);
        return org;
    }

    /// <summary>Seed a single Contact member, optionally configured (name parts, phones, emails).</summary>
    public async Task<Contact> SeedContactAsync(string id, Action<Contact> configure = null)
    {
        var contact = AbstractTypeFactory<Contact>.TryCreateInstance();
        contact.Id = id;
        configure?.Invoke(contact);
        await _provider.GetRequiredService<IMemberService>().SaveChangesAsync([contact]);
        return contact;
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

    /// <summary>Fresh DbContext on the cart DB for seeding/assertions.</summary>
    public CartDbContext NewCartDbContext() => new(_cartOptions);

    /// <summary>Fresh DbContext on the catalog DB for seeding/assertions.</summary>
    public CatalogDbContext NewCatalogDbContext() => new(_catalogOptions);

    /// <summary>
    /// Seed catalog categories (under <see cref="TestCatalogId"/>, which every harness store reports as its catalog)
    /// so the real Top Sellers category filter has a tree to list top-level badges from and expand into a subtree.
    /// Pass <c>parentId = null</c> for a top-level category; the catalog row is created on first call.
    /// </summary>
    public async Task SeedCategoriesAsync(params (string Id, string Name, string ParentId, bool IsActive)[] categories)
    {
        var seedDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using var db = NewCatalogDbContext();

        if (!await db.Set<CatalogEntity>().AnyAsync(x => x.Id == TestCatalogId))
        {
            db.Add(new CatalogEntity { Id = TestCatalogId, Name = "Test Catalog", DefaultLanguage = "en-US", CreatedDate = seedDate, ModifiedDate = seedDate });
        }

        foreach (var category in categories)
        {
            db.Add(new CategoryEntity
            {
                Id = category.Id,
                Name = category.Name,
                Code = category.Id,
                CatalogId = TestCatalogId,
                ParentCategoryId = category.ParentId,
                IsActive = category.IsActive,
                CreatedDate = seedDate,
                ModifiedDate = seedDate,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Seed catalog products (<see cref="ItemEntity"/>) under <see cref="TestCatalogId"/>, each in a category, so the
    /// Top Sellers category filter (option (a): category → product ids via the catalog index, stood in for by the
    /// harness's repo-backed <c>IProductIndexedSearchService</c>) can resolve which sold products fall in a category
    /// subtree. Call <see cref="SeedCategoriesAsync"/> first — the products' categories (and the catalog row) must
    /// already exist.
    /// </summary>
    public async Task SeedProductsAsync(params (string Id, string CategoryId)[] products)
    {
        var seedDate = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        using var db = NewCatalogDbContext();

        if (!await db.Set<CatalogEntity>().AnyAsync(x => x.Id == TestCatalogId))
        {
            db.Add(new CatalogEntity { Id = TestCatalogId, Name = "Test Catalog", DefaultLanguage = "en-US", CreatedDate = seedDate, ModifiedDate = seedDate });
        }

        foreach (var product in products)
        {
            db.Add(new ItemEntity
            {
                Id = product.Id,
                Name = product.Id,
                Code = product.Id,
                CatalogId = TestCatalogId,
                CategoryId = product.CategoryId,
                IsActive = true,
                CreatedDate = seedDate,
                ModifiedDate = seedDate,
            });
        }

        await db.SaveChangesAsync();
    }

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
        _cartConnection.Dispose();
        _catalogConnection.Dispose();
    }
}
