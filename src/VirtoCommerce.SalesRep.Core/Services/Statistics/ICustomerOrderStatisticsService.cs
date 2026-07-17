using System.Collections.Generic;
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

    /// <summary>
    /// Aggregates the same statistics as <see cref="GetStatisticsAsync"/> but grouped per organization — one entry
    /// per organization in <see cref="CustomerOrderStatisticsCriteria.OrganizationIds"/> that has orders in range,
    /// computed in a single grouped query for the whole set rather than one query per organization. Backs the
    /// "My customers" list's inline per-row purchase columns and its order-derived sorts (last-order date, period
    /// total). Organizations with no matching orders are omitted (callers treat them as zero / never-ordered).
    /// </summary>
    Task<IDictionary<string, CustomerOrderStatisticsPeriod>> GetStatisticsByOrganizationAsync(CustomerOrderStatisticsCriteria criteria);
}
