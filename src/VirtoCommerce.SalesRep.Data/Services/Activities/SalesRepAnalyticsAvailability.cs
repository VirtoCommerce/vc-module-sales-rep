using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Exceptions;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class SalesRepAnalyticsAvailability : ISalesRepAnalyticsAvailability
{
    private readonly IOptionalDependency<IAnalyticsService> _analyticsService;
    private readonly ILogger<SalesRepAnalyticsAvailability> _logger;

    public SalesRepAnalyticsAvailability(
        IOptionalDependency<IAnalyticsService> analyticsService,
        ILogger<SalesRepAnalyticsAvailability> logger)
    {
        _analyticsService = analyticsService;
        _logger = logger;
    }

    // Absent, unconfigured and "could not find out" are one answer on purpose: a caller can do nothing different.
    public virtual async Task<bool> IsConfiguredAsync(string storeId)
    {
        if (!_analyticsService.HasValue)
        {
            return false;
        }

        try
        {
            return await _analyticsService.Value.IsConfiguredAsync(storeId);
        }
        catch (AnalyticsException ex)
        {
            _logger.LogWarning(ex, "Could not determine whether Google Analytics is configured for store {StoreId}", storeId);

            return false;
        }
    }
}
