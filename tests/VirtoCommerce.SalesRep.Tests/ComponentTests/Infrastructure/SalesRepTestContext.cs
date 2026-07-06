using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.CustomerModule.Data.Handlers;
using VirtoCommerce.CustomerModule.Data.Repositories;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Security.Events;
using VirtoCommerce.Platform.Security.Repositories;
using VirtoCommerce.SalesRep.Tests.Infrastructure;
using VirtoCommerce.SalesRep.Web.Controllers.Api;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Isolated per-test harness: real SalesRep + platform-security + customer services over two in-memory SQLite
/// databases, wired exactly as the modules wire them (registrations ported from their Module.Initialize).
/// Tests act through the real <see cref="SalesRepController"/> and assert against the databases.
/// </summary>
internal sealed class SalesRepTestContext : IDisposable
{
    private readonly SqliteConnection _securityConnection;
    private readonly SqliteConnection _customerConnection;
    private readonly ServiceProvider _provider;
    private readonly DbContextOptions<SecurityDbContext> _securityOptions;
    private readonly DbContextOptions<CustomerDbContext> _customerOptions;

    private SalesRepTestContext(
        SqliteConnection securityConnection,
        SqliteConnection customerConnection,
        ServiceProvider provider,
        DbContextOptions<SecurityDbContext> securityOptions,
        DbContextOptions<CustomerDbContext> customerOptions)
    {
        _securityConnection = securityConnection;
        _customerConnection = customerConnection;
        _provider = provider;
        _securityOptions = securityOptions;
        _customerOptions = customerOptions;
    }

    public static SalesRepTestContext Create()
    {
        var securityConnection = SqliteTestDbContextFactory.CreateConnection();
        var customerConnection = SqliteTestDbContextFactory.CreateConnection();
        var securityOptions = SqliteTestDbContextFactory.CreateOptions<SecurityDbContext>(securityConnection);
        var customerOptions = SqliteTestDbContextFactory.CreateOptions<CustomerDbContext>(customerConnection);

        var provider = new ServiceCollection()
            .AddSecuritySlice(securityOptions)
            .AddCustomerSlice(customerOptions)
            .AddSalesRepSlice()
            .BuildServiceProvider();

        // Subscribe the customer delete-cascade handler to the in-process bus — mirrors the customer module's
        // appBuilder.RegisterEventHandler<UserChangedEvent, DeleteOrganizationMembershipUserChangedEventHandler>().
        // This is what clears a rep's OrganizationMemberships when its ApplicationUser is deleted.
        provider.GetRequiredService<IEventHandlerRegistrar>()
            .RegisterEventHandler<UserChangedEvent>(provider.GetRequiredService<DeleteOrganizationMembershipUserChangedEventHandler>());

        return new SalesRepTestContext(securityConnection, customerConnection, provider, securityOptions, customerOptions);
    }

    /// <summary>The real REST controller resolved from DI (the test's entry point).</summary>
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

    /// <summary>Fresh DbContext on the customer DB for assertions (avoids tracking conflicts).</summary>
    public CustomerDbContext NewCustomerDbContext() => new(_customerOptions);

    /// <summary>Fresh DbContext on the security DB for assertions.</summary>
    public SecurityDbContext NewSecurityDbContext() => new(_securityOptions);

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
    }
}
