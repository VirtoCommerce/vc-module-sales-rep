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

    // Absent module and unconfigured store are one answer on purpose: a caller can do nothing different
    // about either, and both mean the tracked figures beside it are not measurements.
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
            // The analytics module distinguishes "no property id" from "could not find out", and it is right to.
            // This is where that distinction is spent: a rep screen has one neutral empty state either way, and
            // the question is asked to decide whether to render figures — not to explain the server to a rep.
            _logger.LogWarning(ex, "Could not determine whether Google Analytics is configured for store {StoreId}", storeId);

            return false;
        }
    }
}
