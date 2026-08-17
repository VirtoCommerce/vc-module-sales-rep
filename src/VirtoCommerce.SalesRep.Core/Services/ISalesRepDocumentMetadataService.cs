using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepDocumentMetadataService
{
    Task<IList<SalesRepDocumentMetadata>> GetByIdsAsync(IList<string> ids);

    Task CreateAsync(IList<SalesRepDocumentMetadata> metadata);

    Task SaveAsync(IList<SalesRepDocumentMetadata> metadata);

    Task SetPinnedAsync(string id, bool isPinned);

    Task DeleteAsync(IList<string> ids);
}
