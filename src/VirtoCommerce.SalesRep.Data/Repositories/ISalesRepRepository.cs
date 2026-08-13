using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Data.Models;

namespace VirtoCommerce.SalesRep.Data.Repositories;

public interface ISalesRepRepository : IRepository
{
    IQueryable<DocumentMetadataEntity> DocumentMetadata { get; }

    Task<IList<DocumentMetadataEntity>> GetDocumentMetadataByIdsAsync(IList<string> ids);
}
