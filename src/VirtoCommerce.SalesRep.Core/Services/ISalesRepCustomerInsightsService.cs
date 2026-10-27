using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepCustomerInsightsService
{
    Task<bool> IsAvailableAsync(string storeId);

    Task<IList<SalesRepSearchTerm>> GetSearchTermsAsync(SalesRepCustomerInsightsCriteria criteria);

    Task<IList<SalesRepBrowsedProduct>> GetBrowsedProductsAsync(SalesRepCustomerInsightsCriteria criteria);
}
