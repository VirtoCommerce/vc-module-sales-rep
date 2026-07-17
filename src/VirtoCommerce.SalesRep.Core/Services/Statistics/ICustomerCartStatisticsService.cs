using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services.Statistics;

/// <summary>
/// Computes cart/project-derived statistics for a Sales Rep over one date range (VCST dashboard "Active Projects" /
/// cart widgets). Each call aggregates in the database (sum / count / max, grouped by currency) and converts to the
/// requested currency — it never loads carts into memory. Compose several ranges (and derive period-over-period
/// comparisons) at the GraphQL layer, where a per-range DataLoader ensures each distinct range is queried only once.
/// </summary>
public interface ICustomerCartStatisticsService
{
    Task<CustomerCartStatisticsPeriod> GetStatisticsAsync(CustomerCartStatisticsCriteria criteria);
}
