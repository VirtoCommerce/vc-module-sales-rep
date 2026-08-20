using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepDocumentService : ICrudService<SalesRepDocument>
{
    Task<SalesRepDocument> CreateAsync(string fileId, string category, SalesRepDocumentMetadata metadata = null);

    // Returns null when no library document with the given id exists.
    Task<SalesRepDocument> UpdateMetadataAsync(string id, SalesRepDocumentMetadata metadata);
}
