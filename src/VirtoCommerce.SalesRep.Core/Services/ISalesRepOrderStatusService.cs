using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepOrderStatusService
{
    /// <summary>
    /// The order statuses actually present in the orders the criteria scopes to — the source of the orders-list status
    /// filter vocabulary, so a status introduced outside the platform (e.g. by an ERP sync) is offered as a filter and
    /// a status no order in scope carries is not. Scoping mirrors the orders list (the rep's served organizations and
    /// their own created orders), so every offered status has orders behind it for that rep.
    /// </summary>
    Task<IList<string>> GetUsedStatusesAsync(SalesRepOrderStatusCriteria criteria);
}
