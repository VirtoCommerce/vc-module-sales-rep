using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.OrdersModule.Core.Search.Indexed;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>Harness override hook for <see cref="SalesRepTestContext.Create"/>.</summary>
internal static class IndexedOrderSearchOverride
{
    /// <summary>
    /// Shadows the default indexed order search with <see cref="RecordingIndexedCustomerOrderSearchService"/>,
    /// which behaves identically and additionally records every response group it was called with.
    /// </summary>
    public static void Recording(IServiceCollection services)
    {
        services.AddSingleton<RecordingIndexedCustomerOrderSearchService>();
        services.AddSingleton<IIndexedCustomerOrderSearchService>(sp =>
            sp.GetRequiredService<RecordingIndexedCustomerOrderSearchService>());
    }
}
