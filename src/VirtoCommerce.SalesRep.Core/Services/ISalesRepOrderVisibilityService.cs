using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepOrderVisibilityService
{
    Task<bool> IsVisibleAsync(string userId, CustomerOrder order);

    Task<IList<CustomerOrder>> FilterVisibleAsync(string userId, IList<CustomerOrder> orders);
}
