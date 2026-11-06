using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GraphQL;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.AssetsModule.Data.Repositories;
using VirtoCommerce.CartModule.Data.Repositories;
using VirtoCommerce.CatalogModule.Data.Model;
using VirtoCommerce.CatalogModule.Data.Repositories;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.CustomerModule.Data.Repositories;
using VirtoCommerce.CustomerModule.Data.Search.Indexing;
using VirtoCommerce.FileExperienceApi.Core.Models;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.NotificationsModule.Core.Model;
using VirtoCommerce.NotificationsModule.Core.Model.Search;
using VirtoCommerce.NotificationsModule.Core.Services;
using VirtoCommerce.OrdersModule.Data.Repositories;
using VirtoCommerce.OrdersModule.Data.Search.Indexed;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Platform.Security.Caching;
using VirtoCommerce.Platform.Security.Repositories;
using VirtoCommerce.PushMessages.Core.Models;
using VirtoCommerce.PushMessages.Core.Services;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Models;
using VirtoCommerce.SalesRep.Data.Repositories;
using VirtoCommerce.SalesRep.ExperienceApi;
using VirtoCommerce.SalesRep.Tests.Infrastructure;
using VirtoCommerce.SalesRep.Web.Controllers.Api;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.TaskManagement.Data.Repositories;
using VirtoCommerce.Xapi.Core.Infrastructure;
using CustomerSettings = VirtoCommerce.CustomerModule.Core.ModuleConstants.Settings.General;
using OrderSettings = VirtoCommerce.OrdersModule.Core.ModuleConstants.Settings.General;
using OrdersModuleConstants = VirtoCommerce.OrdersModule.Core.ModuleConstants;
using SalesRepModuleConstants = VirtoCommerce.SalesRep.Core.ModuleConstants;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Isolated per-test harness: the module as the platform runs it, its own <c>Module.Initialize</c> and
/// <c>Module.PostInitialize</c> included, among the real platform and modules around it (<see cref="PlatformModules"/>:
/// settings, accounts, stores, currencies, contacts and organizations, the catalog, orders, carts, marketing, assets,
/// notifications, push messages, task management, X-API, X-Catalog, X-Cart, X-Order), every database an in-memory
/// SQLite one and the search index Lucene's in RAM. Tests seed through those modules' services as an administrator
/// would, act through the module's REST controller and its GraphQL schema executed in-process, and assert on the
/// results, the rows, the messages the modules saved and the SQL they ran.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>The stores <see cref="DefaultStoreId"/> and <see cref="OtherStoreId"/> exist, in USD and EUR (EUR their
/// default, ≠ the USD primary, so a store default visibly wins over the primary fallback), en-US and de-DE, on the
/// catalog <see cref="TestCatalogId"/>, with every store-level setting at its default until a test saves one.</item>
/// <item>The currencies are USD (primary) and EUR at 1.25 (1 EUR = 1.25 USD, the platform's "rate relative to the
/// primary" convention).</item>
/// <item>The Order.Status dictionary is the one <see cref="OrderStatuses"/> lists, as an administrator configured it.</item>
/// <item>Each database lives on one SQLite connection, which isn't thread-safe: a test runs one call at a time.</item>
/// </list>
/// </remarks>
internal sealed class SalesRepTestContext : IDisposable
{
    /// <summary>Catalog id every harness store has; the seeded Top Sellers categories live under it.</summary>
    public const string TestCatalogId = "test-catalog";

    /// <summary>The store a rep is created in by default: a real rep account carries a store.</summary>
    public const string DefaultStoreId = "B2B-store";

    /// <summary>A second store, for the tests about a store other than the rep's.</summary>
    public const string OtherStoreId = "other-store";

    /// <summary>The order statuses the store's Order.Status dictionary lists, in its order.</summary>
    public static readonly string[] OrderStatuses = ["New", "Processing", "Completed", "Cancelled", "Failed"];

    private readonly IServiceProvider _provider;
    // False when a host (an E2E Kestrel app) built and owns the provider; it disposes it then.
    private readonly bool _ownsProvider;
    private readonly string _assetsRootPath;

    private SalesRepTestContext(IServiceProvider provider, bool ownsProvider, string assetsRootPath)
    {
        _provider = provider;
        _ownsProvider = ownsProvider;
        _assetsRootPath = assetsRootPath;
    }

    /// <summary>
    /// What the platform's shipped appsettings.json sets that the hosted code reads; every other option keeps its
    /// default: the store X-API's store query falls back to for a domain no store claims, the GraphQL playground (and
    /// with it schema introspection), the authentication types GraphQL ignores, and the order full-text search, which
    /// the module's order lists read through. The platform's country lists, which it ships in its own content root, are
    /// the copies in <c>Platform/localization</c>. And what the module's README tells a deployment to add: the
    /// documents library's upload scope, as its example declares it.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ConfigurationValues { get; } = new Dictionary<string, string>
    {
        ["VirtoCommerce:Stores:DefaultStore"] = DefaultStoreId,
        ["VirtoCommerce:GraphQLPlayground:Enable"] = "true",
        ["VirtoCommerce:GraphQL:ForbiddenAuthenticationTypes:0"] = "Identity.Application",
        ["VirtoCommerce:CountriesFilePath"] = Path.Combine(AppContext.BaseDirectory, "Platform", "localization", "common", "countries.json"),
        ["VirtoCommerce:CountryRegionsFilePath"] = Path.Combine(AppContext.BaseDirectory, "Platform", "localization", "common", "countriesRegions.json"),
        ["Search:OrderFullTextSearchEnabled"] = "true",
        ["FileUpload:Scopes:0:Scope"] = SalesRepModuleConstants.DocumentsScope,
        ["FileUpload:Scopes:0:MaxFileSize"] = "52428800",
        ["FileUpload:Scopes:0:AllowAnonymousUpload"] = "false",
        ["FileUpload:Scopes:0:AllowedExtensions:0"] = ".pdf",
        ["FileUpload:Scopes:0:AllowedExtensions:1"] = ".doc",
        ["FileUpload:Scopes:0:AllowedExtensions:2"] = ".docx",
        ["FileUpload:Scopes:0:AllowedExtensions:3"] = ".xls",
        ["FileUpload:Scopes:0:AllowedExtensions:4"] = ".xlsx",
        ["FileUpload:Scopes:0:AllowedExtensions:5"] = ".ppt",
        ["FileUpload:Scopes:0:AllowedExtensions:6"] = ".pptx",
        ["FileUpload:Scopes:0:AllowedExtensions:7"] = ".txt",
        ["FileUpload:Scopes:0:AllowedExtensions:8"] = ".csv",
        ["FileUpload:Scopes:0:AllowedExtensions:9"] = ".zip",
        ["FileUpload:Scopes:0:AllowedExtensions:10"] = ".png",
        ["FileUpload:Scopes:0:AllowedExtensions:11"] = ".jpg",
        ["FileUpload:Scopes:0:AllowedExtensions:12"] = ".jpeg",
        ["FileUpload:Scopes:0:AllowedExtensions:13"] = ".gif",
        ["FileUpload:Scopes:0:AllowedExtensions:14"] = ".webp",
    };

    /// <param name="configureOverrides">
    /// Registrations added after the platform's and the module's: a project's override of one of the module's services
    /// (e.g. <see cref="OrderFilterRuleOverride.WithCompositeInactiveStatus"/>), the stand-in for Google's API
    /// (<see cref="TestAnalytics.Register"/>), or an E2E host's shell. The last registration wins.
    /// </param>
    /// <param name="withTaskManagement">Whether VirtoCommerce.TaskManagement is installed; a deployment may leave it out.</param>
    /// <param name="beforeModuleStart">
    /// What the platform's database already held when the module started (an earlier version's roles, say): run after
    /// the platform and the other modules started and before the module's own PostInitialize, which seeds around it.
    /// </param>
    /// <param name="buildHost">
    /// Builds the host from the configured collection instead of a bare application builder over it. The E2E hosts pass
    /// one that copies the registrations into a Kestrel <c>WebApplication</c> with its request pipeline so far, so the
    /// startup below adds the GraphQL endpoints to it and the same services answer real HTTP; the caller then owns that host.
    /// </param>
    public static SalesRepTestContext Create(
        Action<IServiceCollection> configureOverrides = null,
        bool withTaskManagement = true,
        Func<SalesRepTestContext, Task> beforeModuleStart = null,
        Func<IServiceCollection, IApplicationBuilder> buildHost = null)
    {
        // The platform blocks on its startup the same way (and the module's PostInitialize does): a test calls this
        // without a synchronization context.
        return CreateAsync(configureOverrides, withTaskManagement, beforeModuleStart, buildHost).GetAwaiter().GetResult();
    }

    private static async Task<SalesRepTestContext> CreateAsync(
        Action<IServiceCollection> configureOverrides,
        bool withTaskManagement,
        Func<SalesRepTestContext, Task> beforeModuleStart,
        Func<IServiceCollection, IApplicationBuilder> buildHost)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(ConfigurationValues).Build();
        var hosted = new HostedModules(
            TaskManagement: withTaskManagement,
            AssetsRootPath: Path.Combine(Path.GetTempPath(), "VirtoCommerce.SalesRep.Tests", "assets", Guid.NewGuid().ToString("N")));
        var module = TestedModule.Create(configuration);

        var services = new ServiceCollection().AddPlatformModules(configuration, hosted);
        module.Initialize(services);
        // The module's database on SQLite, in place of the one its Initialize builds from the connection string.
        services.AddModuleDatabase<SalesRepDbContext>();
        configureOverrides?.Invoke(services);

        var ownsProvider = buildHost == null;
        var app = ownsProvider
            ? new ApplicationBuilder(services.AddSingleton<IConfiguration>(configuration).BuildServiceProvider())
            : buildHost(services);

        var context = new SalesRepTestContext(app.ApplicationServices, ownsProvider, hosted.AssetsRootPath);

        await PlatformModules.PostInitializeAsync(app, hosted);
        if (beforeModuleStart != null)
        {
            await beforeModuleStart(context);
        }

        module.PostInitialize(app);
        await PlatformModules.StartAsync(app.ApplicationServices);
        await context.SeedAsync();

        return context;
    }

    /// <summary>The real REST controller, created per access from the container as MVC's activator creates it.</summary>
    public SalesRepController Controller => ActivatorUtilities.CreateInstance<SalesRepController>(_provider);

    public T GetRequiredService<T>() where T : notnull => _provider.GetRequiredService<T>();

    /// <summary>The root provider (the Kestrel app's when a host built it).</summary>
    public IServiceProvider Services => _provider;

    /// <summary>The SQL the modules' databases ran; clear it before the action a test measures.</summary>
    public SqlCommandLog Sql => GetRequiredService<SqlCommandLog>();

    /// <summary>
    /// User id of the most recently created Sales Rep. <c>SeedOrder</c> defaults a seeded order's CustomerId to this,
    /// so orders count as "created by the rep" (rep-created orders record the rep's user id as CustomerId) without
    /// every test threading it through.
    /// </summary>
    public string LastCreatedRepUserId { get; private set; }

    /// <summary>Saves a module-level setting, as an administrator does in the platform's settings.</summary>
    public Task SetSettingAsync(SettingDescriptor descriptor, object value)
        => GetRequiredService<ISettingsManager>().SetValueAsync(descriptor.Name, value);

    /// <summary>
    /// Saves the store's <c>Customer.ContactDefaultStatus</c>, which a rep created in that store inherits as their
    /// member status (e.g. "Approved" for B2B-store).
    /// </summary>
    public Task SetStoreContactDefaultStatusAsync(string storeId, string status)
        => SaveStoreAsync(storeId, store => store.Settings.Single(x => x.Name == CustomerSettings.ContactDefaultStatus.Name).Value = status);

    /// <summary>Saves a store's sender address, which the email channel sends from.</summary>
    public Task SetStoreEmailAsync(string storeId, string email)
        => SaveStoreAsync(storeId, store => store.Email = email);

    /// <summary>Saves the stores a store trusts (its TrustedGroups): a rep of one of them may act for it.</summary>
    public Task SetStoreTrustedGroupsAsync(string storeId, params string[] groups)
        => SaveStoreAsync(storeId, store => store.TrustedGroups = [.. groups]);

    /// <summary>Changes a store as an administrator does on the store's page: the store is loaded, changed and saved.</summary>
    public async Task SaveStoreAsync(string storeId, Action<Store> change)
    {
        var stores = GetRequiredService<IStoreService>();
        var store = await stores.GetByIdAsync(storeId) ?? throw new InvalidOperationException($"No store '{storeId}'.");
        change(store);

        await stores.SaveChangesAsync([store]);
    }

    /// <summary>
    /// Translates order statuses in the Order.Status dictionary, as an administrator does in its editor, which saves the
    /// whole dictionary: every status, each (status, language, label) given becoming the status's label in that language.
    /// </summary>
    public async Task TranslateOrderStatusesAsync(params (string Status, string LanguageCode, string Label)[] translations)
    {
        var statuses = await GetRequiredService<ISettingsManager>().GetObjectSettingAsync(OrderSettings.OrderStatus.Name);
        var items = statuses.AllowedValues
            .Select(x => x.ToString())
            .Select(status => new DictionaryItem
            {
                Alias = status,
                LocalizedValues = translations
                    .Where(x => x.Status == status)
                    .Select(x => new LocalizedValue { LanguageCode = x.LanguageCode, Value = x.Label })
                    .ToList(),
            })
            .ToList();

        await GetRequiredService<ILocalizableSettingService>().SaveAsync(OrderSettings.OrderStatus.Name, items);
    }

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
    public Task<Role> CreateGrantingRoleAsync(string name)
        => CreateRoleAsync(name, SalesRepModuleConstants.Security.Permissions.Access);

    /// <summary>
    /// Create a role with an arbitrary permission set (as an admin building custom roles in the Security UI
    /// would), so permission-set-based role seeding can be exercised against pre-existing roles.
    /// </summary>
    public async Task<Role> CreateRoleAsync(string name, params string[] permissions)
    {
        using var roleManager = _provider.GetRequiredService<Func<RoleManager<Role>>>()();
        var role = AbstractTypeFactory<Role>.TryCreateInstance();
        role.Id = Guid.NewGuid().ToString("N");
        role.Name = name;
        role.Permissions = [.. permissions.Select(x => new Permission { Name = x })];

        EnsureSucceeded(await roleManager.CreateAsync(role));

        return role;
    }

    /// <summary>
    /// Create a role that does NOT grant <c>sales-rep:access</c> (e.g. a buyer/manager role an org member could
    /// hold), so behaviors that must preserve unrelated roles/memberships can be exercised.
    /// </summary>
    public Task<Role> CreateNonGrantingRoleAsync(string name) => CreateRoleAsync(name, "customer:read");

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

        EnsureSucceeded(await userManager.CreateAsync(user, "P@ssw0rd123!"));

        return user.Id;
    }

    /// <summary>A signed-in account with no contact, roles or memberships - an ordinary customer.</summary>
    public Task<string> CreateCustomerAccountAsync(string email = "customer@test.com")
    {
        return CreateAccountWithoutRolesAsync(memberId: null, email);
    }

    /// <summary>An account with a caller-chosen id, for tests that pin the user id in a constant.</summary>
    public async Task EnsureAccountAsync(string userId)
    {
        using var userManager = _provider.GetRequiredService<Func<UserManager<ApplicationUser>>>()();
        if (await userManager.FindByIdAsync(userId) != null)
        {
            return;
        }

        var user = AbstractTypeFactory<ApplicationUser>.TryCreateInstance();
        user.Id = userId;
        user.UserName = $"{userId}@test.com";
        user.Email = user.UserName;
        user.UserType = "Customer";

        EnsureSucceeded(await userManager.CreateAsync(user, "P@ssw0rd123!"));
    }

    /// <summary>
    /// Mark an account as a platform administrator. An administrator is not bound to a store, which is the only
    /// way a caller carrying no StoreId passes <c>ISalesRepStoreAccessService</c>.
    /// </summary>
    public async Task MakeAdministratorAsync(string userId)
    {
        using var userManager = _provider.GetRequiredService<Func<UserManager<ApplicationUser>>>()();

        // A detached clone, never the FindByIdAsync instance: that one is the cached (and possibly tracked) user.
        var user = (await userManager.FindByIdAsync(userId)).CloneTyped();
        user.IsAdministrator = true;

        EnsureSucceeded(await userManager.UpdateAsync(user));
    }

    /// <summary>Delete the login account, leaving any member and membership rows behind.</summary>
    public async Task DeleteAccountAsync(string userId)
    {
        using var userManager = _provider.GetRequiredService<Func<UserManager<ApplicationUser>>>()();
        var user = await userManager.FindByIdAsync(userId);

        await userManager.DeleteAsync(user);
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
    /// trigger re-stamps the date.
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
        => CreateRepInStoreAsync(firstName, lastName, email, DefaultStoreId, organizationIds);

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
    /// Populate the (in-memory Lucene) order search index for the given order ids using the real order document
    /// builder, so the customer-orders queries — which read the index, not the DB — return results.
    /// </summary>
    public async Task IndexOrdersAsync(params string[] orderIds)
    {
        var documentBuilder = (IIndexDocumentBuilder)_provider.GetRequiredService<CustomerOrderDocumentBuilder>();
        var documents = await documentBuilder.GetDocumentsAsync(orderIds, CancellationToken.None);
        var searchProvider = _provider.GetRequiredService<ISearchProvider>();
        await searchProvider.IndexAsync(OrdersModuleConstants.OrderIndexDocumentType, documents);
    }

    /// <summary>
    /// Execute a GraphQL query string against the real Sales Rep scoped schema as an authenticated caller.
    /// <paramref name="permissions"/> become platform "permission" claims (as the auth token carries them);
    /// <paramref name="isAdministrator"/> adds the platform Administrator role claim.
    /// Returns the serialized GraphQL response (data + errors) for assertions.
    /// </summary>
    public Task<string> ExecuteGraphQlAsync(string query, string userId = null, string organizationId = null, string[] permissions = null, bool isAdministrator = false, string memberId = null)
    {
        var identity = new ClaimsIdentity(authenticationType: "Test"); // non-null type => IsAuthenticated
        if (!string.IsNullOrEmpty(userId))
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        // Task ownership keys on the contact id, which the platform issues as this claim for every principal.
        if (memberId != null)
        {
            identity.AddClaim(new Claim(PlatformConstants.Security.Claims.MemberIdClaimType, memberId));
        }

        if (!string.IsNullOrEmpty(organizationId))
        {
            identity.AddClaim(new Claim("organization_id", organizationId));
        }

        foreach (var permission in permissions ?? [])
        {
            identity.AddClaim(new Claim(PlatformConstants.Security.Claims.PermissionClaimType, permission));
        }

        if (isAdministrator)
        {
            identity.AddClaim(new Claim(identity.RoleClaimType, PlatformConstants.Security.SystemRoles.Administrator));
        }

        return ExecuteGraphQlInternalAsync(query, new ClaimsPrincipal(identity));
    }

    /// <summary>Execute a GraphQL query string as an anonymous (unauthenticated) caller.</summary>
    public Task<string> ExecuteGraphQlAnonymousAsync(string query)
    {
        return ExecuteGraphQlInternalAsync(query, new ClaimsPrincipal(new ClaimsIdentity()));
    }

    // The module's own schema (/graphql/sales-rep), built from its schema builders only, executed as the GraphQL
    // middleware executes a request: in a DI scope of its own, within the HTTP request it came with (a JSON POST by the
    // caller), which X-API's content type rule, the request cache and the audit stamps read. Serialized with X-API's own
    // serializer, as the endpoint answers.
    private async Task<string> ExecuteGraphQlInternalAsync(string query, ClaimsPrincipal principal)
    {
        await using var scope = _provider.CreateAsyncScope();

        var httpContextAccessor = _provider.GetRequiredService<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider, User = principal };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/json";
        httpContextAccessor.HttpContext = httpContext;

        try
        {
            var executer = scope.ServiceProvider.GetRequiredService<IDocumentExecuter>();
            var serializer = scope.ServiceProvider.GetRequiredService<IGraphQLTextSerializer>();
            var schema = _provider.GetRequiredService<ScopedSchemaFactory<XapiAssemblyMarker>>();

            var result = await executer.ExecuteAsync(options =>
            {
                options.Schema = schema;
                options.Query = query;
                options.RequestServices = scope.ServiceProvider;
                options.UserContext = new GraphQLUserContext(principal);
            });

            return serializer.Serialize(result);
        }
        finally
        {
            httpContextAccessor.HttpContext = null;
        }
    }

    /// <summary>The requested field of a GraphQL response's <c>data</c> node, after asserting the response carries no errors.</summary>
    public static JsonElement Node(string json, string field)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        root.TryGetProperty("errors", out _).Should().BeFalse("GraphQL response should carry no errors: {0}", json);
        return root.GetProperty("data").GetProperty(field).Clone();
    }

    /// <summary>
    /// The email messages the notifications module saved (a scheduled notification is rendered and saved as a message
    /// before it is sent; this platform has no email gateway), oldest first.
    /// </summary>
    public async Task<IList<EmailNotificationMessage>> GetEmailMessagesAsync()
    {
        var criteria = AbstractTypeFactory<NotificationMessageSearchCriteria>.TryCreateInstance();
        criteria.Take = int.MaxValue;
        var messages = await GetRequiredService<INotificationMessageSearchService>().SearchMessageAsync(criteria);

        return messages.Results.OfType<EmailNotificationMessage>().OrderBy(x => x.CreatedDate).ToList();
    }

    /// <summary>The push messages the push messages module saved.</summary>
    public async Task<IList<PushMessage>> GetPushMessagesAsync()
    {
        var criteria = AbstractTypeFactory<PushMessageSearchCriteria>.TryCreateInstance();
        criteria.Take = int.MaxValue;

        return (await GetRequiredService<IPushMessageSearchService>().SearchAsync(criteria)).Results;
    }

    /// <summary>The files the assets' blob provider holds, as blob URLs relative to its root.</summary>
    public IList<string> GetBlobUrls()
    {
        return Directory.Exists(_assetsRootPath)
            ? Directory.EnumerateFiles(_assetsRootPath, "*", SearchOption.AllDirectories)
                .Select(x => Path.GetRelativePath(_assetsRootPath, x).Replace('\\', '/'))
                .ToList()
            : [];
    }

    /// <summary>The assets' folder on disk, for a test that makes the file system refuse a change.</summary>
    public string AssetsRootPath => _assetsRootPath;

    /// <summary>Fresh DbContext on a module's database, for seeding and for asserting on rows (no tracking conflicts).</summary>
    public TContext NewDbContext<TContext>() where TContext : DbContext
        => (TContext)Activator.CreateInstance(typeof(TContext), GetRequiredService<DbContextOptions<TContext>>())!;

    /// <summary>Fresh DbContext on the customer DB for assertions (avoids tracking conflicts).</summary>
    public CustomerDbContext NewCustomerDbContext() => NewDbContext<CustomerDbContext>();

    /// <summary>Fresh DbContext on the security DB for assertions.</summary>
    public SecurityDbContext NewSecurityDbContext() => NewDbContext<SecurityDbContext>();

    /// <summary>Fresh DbContext on the order DB for seeding/assertions.</summary>
    public OrderDbContext NewOrderDbContext() => NewDbContext<OrderDbContext>();

    /// <summary>Fresh DbContext on the cart DB for seeding/assertions.</summary>
    public CartDbContext NewCartDbContext() => NewDbContext<CartDbContext>();

    /// <summary>Fresh DbContext on the catalog DB for seeding/assertions.</summary>
    public CatalogDbContext NewCatalogDbContext() => NewDbContext<CatalogDbContext>();

    /// <summary>Fresh DbContext on the assets DB for seeding/assertions.</summary>
    public AssetsDbContext NewAssetsDbContext() => NewDbContext<AssetsDbContext>();

    /// <summary>Fresh DbContext on the sales-rep DB (document metadata) for assertions.</summary>
    public SalesRepDbContext NewSalesRepDbContext() => NewDbContext<SalesRepDbContext>();

    /// <summary>Fresh DbContext on the task-management DB (WorkTask rows) for assertions.</summary>
    public TaskManagementDbContext NewTaskManagementDbContext()
        => _provider.GetService<DbContextOptions<TaskManagementDbContext>>() is { } options
            ? new TaskManagementDbContext(options)
            : throw new InvalidOperationException("The context was built without the task-management module.");

    /// <summary>
    /// Backdate a document's metadata creation date (the documents default sort key) so ordering tests are
    /// deterministic. Direct UPDATE (no audit re-stamp) + expiry of the metadata CRUD/search cache regions the
    /// raw write bypasses.
    /// </summary>
    public async Task SetDocumentCreatedDateAsync(string documentId, DateTime createdDate)
    {
        using var db = NewSalesRepDbContext();
        await db.Set<DocumentMetadataEntity>()
            .Where(x => x.Id == documentId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.CreatedDate, createdDate));

        GenericSearchCachingRegion<SalesRepDocumentMetadata>.ExpireRegion();
        GenericCachingRegion<SalesRepDocumentMetadata>.ExpireRegion();
    }

    /// <summary>
    /// Simulate out-of-band corruption: the AssetEntry row vanishes WITHOUT AssetEntryChangedEvent (raw SQL /
    /// a mid-cascade failure), so the metadata cascade never runs. Raw DELETE + expiry of the AssetEntry
    /// CRUD/search cache regions the raw write bypasses.
    /// </summary>
    public async Task DeleteAssetEntryRowAsync(string fileId)
    {
        using var db = NewAssetsDbContext();
        await db.Database.ExecuteSqlAsync($"""DELETE FROM "AssetEntry" WHERE "Id" = {fileId}""");

        GenericSearchCachingRegion<AssetEntry>.ExpireRegion();
        GenericCachingRegion<AssetEntry>.ExpireRegion();
    }

    /// <summary>
    /// The real two-step document intake: upload the bytes to the sales-rep-documents scope through the REAL
    /// file-experience-api upload service, then register the uploaded file in the library.
    /// </summary>
    public async Task<SalesRepDocument> UploadDocumentAsync(string fileName, string category, SalesRepDocumentMetadata metadata = null, string content = "content")
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var uploadResult = await GetRequiredService<IFileUploadService>().UploadFileAsync(new FileUploadRequest
        {
            Scope = SalesRepModuleConstants.DocumentsScope,
            UserId = "test-user",
            FileName = fileName,
            Stream = stream,
        });

        if (!uploadResult.Succeeded)
        {
            throw new InvalidOperationException(uploadResult.ErrorMessage);
        }

        return await GetRequiredService<ISalesRepDocumentService>().CreateAsync(uploadResult.Id, category, metadata);
    }

    /// <summary>
    /// Seed catalog categories (under <see cref="TestCatalogId"/>, which every harness store has as its catalog) so the
    /// real Top Sellers category filter has a tree to list top-level badges from and expand into a subtree. Pass
    /// <c>parentId = null</c> for a top-level category; the catalog row is created on first call.
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

        await db.SaveChangesKeepingDatesAsync();
    }

    /// <summary>Unwraps the value from a controller action result (actions return <c>Ok(value)</c>).</summary>
    public static T Unwrap<T>(ActionResult<T> result)
    {
        var value = result.Result is OkObjectResult ok ? (T)ok.Value : result.Value;
        if (value is null)
        {
            // A failed call names its reason here instead of surfacing later as a null.
            var answer = result.Result is ObjectResult objectResult
                ? $"{objectResult.StatusCode}: {objectResult.Value}"
                : result.Result?.GetType().Name ?? "no result";
            throw new InvalidOperationException($"The controller answered without a value ({answer}).");
        }

        return value;
    }

    /// <summary>
    /// What every test starts with, set up through the core, store and settings services as an administrator sets up a
    /// platform: the currencies, the stores and the Order.Status dictionary.
    /// </summary>
    private async Task SeedAsync()
    {
        await GetRequiredService<ICurrencyService>().SaveChangesAsync(
        [
            new Currency { Code = "USD", Name = "US Dollar", Symbol = "$", ExchangeRate = 1m, IsPrimary = true },
            new Currency { Code = "EUR", Name = "Euro", Symbol = "€", ExchangeRate = 1.25m },
        ]);

        await GetRequiredService<IStoreService>().SaveChangesAsync([NewStore(DefaultStoreId), NewStore(OtherStoreId)]);

        var settingsManager = GetRequiredService<ISettingsManager>();
        var orderStatuses = await settingsManager.GetObjectSettingAsync(OrderSettings.OrderStatus.Name);
        orderStatuses.AllowedValues = [.. OrderStatuses];
        await settingsManager.SaveObjectSettingsAsync([orderStatuses]);

        // With the analytics module hosted (and Google stood in), its property, as an administrator configures it.
        if (_provider.GetService<TestAnalytics>() is { } analytics)
        {
            await analytics.ConfigureAsync(this, [DefaultStoreId, OtherStoreId]);
        }
    }

    private static Store NewStore(string storeId) => new()
    {
        Id = storeId,
        Name = storeId,
        Catalog = TestCatalogId,
        DefaultLanguage = "en-US",
        Languages = ["en-US", "de-DE"],
        DefaultCurrency = "EUR",
        Currencies = ["USD", "EUR"],
    };

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    public void Dispose()
    {
        if (_ownsProvider)
        {
            // The provider owns the databases (ModuleDatabases), so they go with it.
            (_provider as IDisposable)?.Dispose();
        }

        try
        {
            if (Directory.Exists(_assetsRootPath))
            {
                Directory.Delete(_assetsRootPath, recursive: true);
            }
        }
        catch (IOException)
        {
            // A file a failing test left open; the temp folder is the OS's to clean.
        }
    }
}
