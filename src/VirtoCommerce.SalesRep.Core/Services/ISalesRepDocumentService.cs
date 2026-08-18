using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepDocumentService
{
    Task<SalesRepDocument> CreateAsync(string fileId, string category, SalesRepDocumentMetadata metadata = null);

    Task<SalesRepDocument> UpdateMetadataAsync(string id, SalesRepDocumentMetadata metadata);

    Task DeleteAsync(IList<string> ids);

    Task<SalesRepDocument> GetAsync(string id);
}
