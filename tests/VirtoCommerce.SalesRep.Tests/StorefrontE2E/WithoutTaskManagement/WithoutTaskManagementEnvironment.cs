using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;
using VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.WithoutTaskManagement;

/// <summary>
/// Group 5: a rep and one customer on a storefront whose module list does not name VirtoCommerce.TaskManagement, so
/// the hub leaves out what needs it.
/// </summary>
public sealed class WithoutTaskManagementEnvironment : StorefrontEnvironment
{
    public const string NorthwindId = "e2e-org-northwind";

    public string RepUserId { get; private set; }

    private protected override void ConfigureServices(IServiceCollection services)
        => services.AddSingleton<IModuleService>(new StorefrontModuleService(taskManagementInstalled: false));

    private protected override async Task SeedAsync(SalesRepTestContext context)
    {
        await context.SeedOrganizationAsync(NorthwindId, org => org.Name = "Northwind Traders");

        var rep = await context.CreateRepInStoreAsync("Una", "Rep", "una.rep@e2e.local", InProcessBackend.StoreId, NorthwindId);
        RepUserId = rep.UserId;

        await context.IndexMembersAsync(NorthwindId);
    }
}
