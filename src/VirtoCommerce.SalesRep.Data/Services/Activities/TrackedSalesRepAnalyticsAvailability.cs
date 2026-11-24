using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class TrackedSalesRepAnalyticsAvailability : SalesRepAnalyticsAvailability
{
    private readonly ISalesRepPlatformAnalytics _platformAnalytics;

    public TrackedSalesRepAnalyticsAvailability(
        IOptionalDependency<IAnalyticsService> analyticsService,
        ILogger<SalesRepAnalyticsAvailability> logger,
        ISalesRepPlatformAnalytics platformAnalytics)
        : base(analyticsService, logger)
    {
        _platformAnalytics = platformAnalytics;
    }

    public override async Task<bool> IsConfiguredAsync(string storeId)
    {
        return await _platformAnalytics.GetSearchServiceAsync(storeId) != null || await base.IsConfiguredAsync(storeId);
    }
}
