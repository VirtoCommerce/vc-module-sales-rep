using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.OrdersModule.Core.Search.Indexed;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

internal static class IndexedOrderSearchOverride
{
    public static void Recording(IServiceCollection services)
    {
        services.AddSingleton<RecordingIndexedCustomerOrderSearchService>();
        services.AddSingleton<IIndexedCustomerOrderSearchService>(sp =>
            sp.GetRequiredService<RecordingIndexedCustomerOrderSearchService>());
    }
}
