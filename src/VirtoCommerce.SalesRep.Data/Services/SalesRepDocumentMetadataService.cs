using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;
using VirtoCommerce.Platform.Caching;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Common;
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
        IEventPublisher eventPublisher,
        AbstractValidator<SalesRepDocumentMetadata> validator)
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

    // The single-pin invariant is enforced by the database — the repository pins the target and clears every
    // other pin in ONE atomic UPDATE, so no lock is needed and concurrent pins converge. This is the pin
    // column's only writer: the entity's FromModel/Patch never copy IsPinned, so no metadata save can touch it.
    public virtual async Task<bool> SetPinnedAsync(string id, bool isPinned)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        using var repository = repositoryFactory();

        var found = await repository.SetDocumentPinnedAsync(id, isPinned);

        if (found)
        {
            // The set-based write bypasses the CrudService pipeline, so expire the cache regions it would have.
            GenericCachingRegion<SalesRepDocumentMetadata>.ExpireRegion();
            GenericSearchCachingRegion<SalesRepDocumentMetadata>.ExpireRegion();
        }

        return found;
    }
}
