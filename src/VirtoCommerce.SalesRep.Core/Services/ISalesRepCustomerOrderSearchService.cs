using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;

namespace VirtoCommerce.SalesRep.Core.Services;

/// <summary>
/// The platform customer-order search extended with a batched "latest order per organization" lookup used by the
/// Sales Rep "My customers" list (VCST-5304), so the last order for a whole page of customers is resolved in one
/// query instead of one query per row. Registered under this interface only — it does not replace the platform-wide
/// <see cref="ICustomerOrderSearchService"/>.
/// </summary>
public interface ISalesRepCustomerOrderSearchService : ICustomerOrderSearchService
{
    /// <summary>
    /// Returns the most recent order (by created date) for each of the specified organizations, resolved with a
    /// single grouped database query. Organizations without orders are omitted; prototype orders are excluded.
    /// </summary>
    Task<IDictionary<string, CustomerOrder>> GetLatestOrdersByOrganizationIdsAsync(IList<string> organizationIds);
}
