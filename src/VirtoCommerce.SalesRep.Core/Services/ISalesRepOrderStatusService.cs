using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepOrderStatusService
{
    /// <summary>
    /// The statuses the scoped orders actually carry — the orders-list filter vocabulary. A status introduced outside
    /// the platform (e.g. an ERP sync) is therefore filterable, and one no order in scope carries is not offered.
    /// </summary>
    Task<IList<string>> GetUsedStatusesAsync(SalesRepScopeCriteria criteria);
}
