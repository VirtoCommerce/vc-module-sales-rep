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

    public virtual IQueryable<DocumentMetadataEntity> DocumentMetadata => DbContext.Set<DocumentMetadataEntity>();

    public virtual async Task<IList<DocumentMetadataEntity>> GetDocumentMetadataByIdsAsync(IList<string> ids, string responseGroup = null)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        return await DocumentMetadata.Where(x => ids.Contains(x.Id)).ToListAsync();
    }

    public virtual async Task<bool> SetDocumentPinnedAsync(string id, bool isPinned)
    {
        // The existence check guards the return value AND keeps a pin of a missing id from clearing the
        // current pin (the UPDATE below would otherwise still match the pinned rows).
        if (!await DocumentMetadata.AnyAsync(x => x.Id == id))
        {
            return false;
        }

        if (isPinned)
        {
            // ONE atomic statement pins the target and clears every other pin (SET IsPinned = (Id = @id) over
            // the union of both), so concurrent pins converge to a single pinned row without any lock — a
            // clear-then-set statement pair could interleave into two pinned rows.
            await DocumentMetadata
                .Where(x => x.Id == id || x.IsPinned)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsPinned, x => x.Id == id));
        }
        else
        {
            await DocumentMetadata
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.IsPinned, false));
        }

        return true;
    }
}
