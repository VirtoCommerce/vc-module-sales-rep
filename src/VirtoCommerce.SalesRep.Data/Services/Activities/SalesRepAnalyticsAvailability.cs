using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class SalesRepAnalyticsAvailability : ISalesRepAnalyticsAvailability
{
    private readonly IOptionalDependency<IAnalyticsService> _analyticsService;

    public SalesRepAnalyticsAvailability(IOptionalDependency<IAnalyticsService> analyticsService)
    {
        _analyticsService = analyticsService;
    }

    // Absent module and unconfigured store are one answer on purpose: a caller can do nothing different
    // about either, and both mean the tracked figures beside it are not measurements.
    public virtual async Task<bool> IsConfiguredAsync(string storeId)
    {
        return _analyticsService.HasValue && await _analyticsService.Value.IsConfiguredAsync(storeId);
    }
}
