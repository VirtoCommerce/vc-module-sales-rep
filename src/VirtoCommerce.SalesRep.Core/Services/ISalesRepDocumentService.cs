using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepDocumentService
{
    Task<SalesRepDocument> UploadAsync(Stream stream, string fileName, string category, SalesRepDocumentMetadata metadata = null);

    // Updates the metadata of a pre-existing library document; an unknown id is a not-found (KeyNotFoundException).
    Task<SalesRepDocument> UpdateMetadataAsync(string id, SalesRepDocumentMetadata metadata);

    Task DeleteAsync(IList<string> ids);

    Task<SalesRepDocument> GetAsync(string id);

    Task<Stream> OpenReadAsync(string id);
}
