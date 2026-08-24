using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.Platform.Data.GenericCrud;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Events;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Models;
using VirtoCommerce.SalesRep.Data.Repositories;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepDocumentMetadataService(
        Func<ISalesRepRepository> repositoryFactory,
        IPlatformMemoryCache platformMemoryCache,
        IEventPublisher eventPublisher,
        AbstractValidator<SalesRepDocumentMetadata> validator,
        IDistributedLockService distributedLockService)
    : CrudService<SalesRepDocumentMetadata, DocumentMetadataEntity, DocumentMetadataChangingEvent, DocumentMetadataChangedEvent>(
        repositoryFactory,
        platformMemoryCache,
        eventPublisher),
    ISalesRepDocumentMetadataService
{
    protected override Task<IList<DocumentMetadataEntity>> LoadEntities(IRepository repository, IList<string> ids, string responseGroup)
    {
        return ((ISalesRepRepository)repository).GetDocumentMetadataByIdsAsync(ids, responseGroup);
    }

    protected override async Task BeforeSaveChanges(IList<SalesRepDocumentMetadata> models)
    {
        foreach (var model in models)
        {
            model.Name = model.Name?.Trim();
            model.Category = model.Category?.Trim();
        }

        await ValidateAsync(models);

        await base.BeforeSaveChanges(models);
    }

    protected virtual async Task ValidateAsync(IList<SalesRepDocumentMetadata> models)
    {
        foreach (var model in models)
        {
            await validator.ValidateAndThrowAsync(model);
        }
    }

    // The lock serializes the read-modify-write; filtered unique indexes are not portable across the three providers.
    // The x-api IDistributedLockService (unlike the platform one, whose non-Redis binding is a pass-through) falls
    // back to a real per-key in-process lock without Redis, so single-instance deployments stay guarded too.
    public virtual Task<bool> SetPinnedAsync(string id, bool isPinned)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return distributedLockService.ExecuteAsync(ModuleConstants.Documents.PinLockKey, () => SetPinnedInternalAsync(id, isPinned));
    }

    protected virtual async Task<bool> SetPinnedInternalAsync(string id, bool isPinned)
    {
        var target = (await GetAsync([id])).FirstOrDefault();
        if (target == null)
        {
            return false;
        }

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

        return true;
    }
}
