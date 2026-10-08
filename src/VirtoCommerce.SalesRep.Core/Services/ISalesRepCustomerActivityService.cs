using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepCustomerActivityService
{
    Task<SalesRepCustomerActivitySummary> GetSummaryAsync(SalesRepCustomerActivityCriteria criteria);
}
