using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services.Statistics;

/// <summary>
/// Computes order-derived sales statistics for a single customer organization over one date range (VCST-5309).
/// Backs the Sales Rep customer-profile widgets (YTD purchases, average order value, orders count). Each call
/// aggregates in the database (sum / count / max, grouped by currency) and converts to the requested currency —
/// it never loads orders into memory. Compose several ranges (and derive period-over-period comparisons) at the
/// GraphQL layer, where a per-range DataLoader ensures each distinct range is queried only once per request.
/// </summary>
public interface ICustomerOrderStatisticsService
{
    Task<CustomerOrderStatisticsPeriod> GetStatisticsAsync(CustomerOrderStatisticsCriteria criteria);
}
