using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepTopSellerService
{
    Task<IList<SalesRepTopSeller>> GetTopSellersAsync(SalesRepTopSellerCriteria criteria);

    Task<IList<string>> GetSoldProductIdsAsync(SalesRepTopSellerCriteria criteria);
}
