using System.Threading.Tasks;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepAnalyticsDiagnosticsService
{
    Task<AnalyticsDiagnosticsResult> RunAsync(string storeId, bool includeLiveData);
}
