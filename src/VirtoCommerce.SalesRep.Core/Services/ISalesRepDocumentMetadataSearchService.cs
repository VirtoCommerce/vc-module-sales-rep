using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepDocumentMetadataSearchService : ISearchService<SalesRepDocumentMetadataSearchCriteria, SalesRepDocumentMetadataSearchResult, SalesRepDocumentMetadata>
{
    Task<IList<SalesRepDocumentCategory>> GetCategoryCountsAsync(string keyword = null);
}
