using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepDocumentSearchService : ISearchService<SalesRepDocumentSearchCriteria, SalesRepDocumentSearchResult, SalesRepDocument>
{
    Task<IList<SalesRepDocumentCategory>> GetCategoriesAsync(string keyword = null);
}
