using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Services;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepCustomerOrderSearchService : ICustomerOrderSearchService
{
    Task<IDictionary<string, CustomerOrder>> GetLatestOrdersByOrganizationIdsAsync(IList<string> organizationIds, string customerId, string storeId, string responseGroup);
}
