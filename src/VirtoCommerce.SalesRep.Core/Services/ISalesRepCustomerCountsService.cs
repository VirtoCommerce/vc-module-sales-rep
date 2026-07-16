using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// Computes the Sales Rep "my customers" counters over one date range (dashboard "My Customers" widget: customers
/// who ordered in the period, and customers new in the period). Aggregates in the database over the rep's own
/// orders — never loads orders into memory. Compose several ranges (and period-over-period comparisons) at the
/// GraphQL layer, where a per-range DataLoader ensures each distinct range is queried only once per request.
/// </summary>
public interface ISalesRepCustomerCountsService
{
    Task<SalesRepCustomerCountsPeriod> GetCountsAsync(SalesRepCustomerCountsCriteria criteria);
}
