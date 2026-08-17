using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.GenericCrud;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepDocumentMetadataService : ICrudService<SalesRepDocumentMetadata>
{
    // Pins the document and clears the pin on every other row (a single pinned document at most).
    Task SetPinnedAsync(string id, bool isPinned);
}
