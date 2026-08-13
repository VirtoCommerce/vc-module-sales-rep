using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Platform.Data.Infrastructure;
using VirtoCommerce.SalesRep.Data.Models;

namespace VirtoCommerce.SalesRep.Data.Repositories;

public class SalesRepRepository : DbContextRepositoryBase<SalesRepDbContext>, ISalesRepRepository
{
    public SalesRepRepository(SalesRepDbContext dbContext, IUnitOfWork unitOfWork = null)
        : base(dbContext, unitOfWork)
    {
    }

    public IQueryable<DocumentMetadataEntity> DocumentMetadata => DbContext.Set<DocumentMetadataEntity>();

    public async Task<IList<DocumentMetadataEntity>> GetDocumentMetadataByIdsAsync(IList<string> ids)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return await DocumentMetadata.Where(x => ids.Contains(x.Id)).ToListAsync();
    }
}
