using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

internal static class SalesRepAccessOverride
{
    public static void HidingOneOrganization(IServiceCollection services)
        => services.AddTransient<ISalesRepOrganizationAccessService, OrganizationAccessOverride>();
}
