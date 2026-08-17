using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepDocumentSearchService
{
    Task<SalesRepDocumentSearchResult> SearchAsync(SalesRepDocumentSearchCriteria criteria);

    Task<IList<SalesRepDocumentCategory>> GetCategoriesAsync(string keyword = null);
}
