using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.SalesRep.Core.Events;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Models;
using VirtoCommerce.SalesRep.Data.Repositories;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepDocumentMetadataService(
        Func<ISalesRepRepository> repositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        IEventPublisher eventPublisher)
    : CrudService<SalesRepDocumentMetadata, DocumentMetadataEntity, DocumentMetadataChangingEvent, DocumentMetadataChangedEvent>(
        repositoryFactory,
        platformMemoryCache,
        eventPublisher),
    ISalesRepDocumentMetadataService
{
    protected override Task<IList<DocumentMetadataEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((ISalesRepRepository)repository).GetDocumentMetadataByIdsAsync(ids);
    }

    protected override async Task BeforeSaveChanges(IList<SalesRepDocumentMetadata> models)
    {
        await base.BeforeSaveChanges(models);

        if (models.Any(x => string.IsNullOrEmpty(x.Id)))
        {
            throw new ArgumentException("Document metadata requires the document id.", nameof(models));
        }

        foreach (var model in models)
        {
            model.Category = SalesRepDocumentCategoryValidator.Sanitize(model.Category, required: false);
        }
    }

    public virtual async Task SetPinnedAsync(string id, bool isPinned)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        var target = (await GetAsync([id])).FirstOrDefault()
            ?? throw new KeyNotFoundException($"Document '{id}' was not found in the library.");

        target.IsPinned = isPinned;
        var toSave = new List<SalesRepDocumentMetadata> { target };

        // At most one document is pinned: pinning one clears the pin on every other row.
        if (isPinned)
        {
            List<string> otherPinnedIds;
            using (var repository = repositoryFactory())
            {
                otherPinnedIds = await repository.DocumentMetadata
                    .Where(x => x.IsPinned && x.Id != id)
                    .Select(x => x.Id)
                    .ToListAsync();
            }

            if (otherPinnedIds.Count > 0)
            {
                var others = await GetAsync(otherPinnedIds);
                foreach (var other in others)
                {
                    other.IsPinned = false;
                }

                toSave.AddRange(others);
            }
        }

        await SaveChangesAsync(toSave);
    }
}
