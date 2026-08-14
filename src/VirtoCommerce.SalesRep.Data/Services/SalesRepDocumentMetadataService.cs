using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Caching;
using VirtoCommerce.SalesRep.Data.Models;
using VirtoCommerce.SalesRep.Data.Repositories;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepDocumentMetadataService : ISalesRepDocumentMetadataService
{
    private readonly Func<ISalesRepRepository> _repositoryFactory;

    public SalesRepDocumentMetadataService(Func<ISalesRepRepository> repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public virtual async Task<IList<SalesRepDocumentMetadata>> GetByIdsAsync(IList<string> ids)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        using var repository = _repositoryFactory();
        var entities = await repository.GetDocumentMetadataByIdsAsync(ids);

        return entities
            .Select(x => x.ToModel(AbstractTypeFactory<SalesRepDocumentMetadata>.TryCreateInstance()))
            .ToList();
    }

    public virtual async Task SaveAsync(IList<SalesRepDocumentMetadata> metadata)
    {
        if (metadata.IsNullOrEmpty())
        {
            return;
        }

        if (metadata.Any(x => string.IsNullOrEmpty(x.Id)))
        {
            throw new ArgumentException("Document metadata requires the document id.", nameof(metadata));
        }

        foreach (var model in metadata)
        {
            model.Category = SalesRepDocumentCategoryValidator.Sanitize(model.Category, required: false);
        }

        using var repository = _repositoryFactory();
        var ids = metadata.Select(x => x.Id).ToList();
        var existingEntities = await repository.GetDocumentMetadataByIdsAsync(ids);
        var pkMap = new PrimaryKeyResolvingMap();
        var savedEntities = new List<DocumentMetadataEntity>();

        foreach (var model in metadata)
        {
            var sourceEntity = AbstractTypeFactory<DocumentMetadataEntity>.TryCreateInstance().FromModel(model, pkMap);
            var targetEntity = existingEntities.FirstOrDefault(x => x.Id == model.Id);

            if (targetEntity != null)
            {
                // Pin state is owned by SetPinnedAsync — a full-replace save must not change it.
                sourceEntity.IsPinned = targetEntity.IsPinned;
                sourceEntity.Patch(targetEntity);
                savedEntities.Add(targetEntity);
            }
            else
            {
                sourceEntity.IsPinned = false;
                repository.Add(sourceEntity);
                savedEntities.Add(sourceEntity);
            }
        }

        await repository.UnitOfWork.CommitAsync();
        pkMap.ResolvePrimaryKeys();

        SalesRepDocumentCacheRegion.ExpireRegion();
    }

    public virtual async Task SetPinnedAsync(string id, bool isPinned)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        using var repository = _repositoryFactory();
        var entity = (await repository.GetDocumentMetadataByIdsAsync([id])).FirstOrDefault();

        if (entity == null)
        {
            entity = AbstractTypeFactory<DocumentMetadataEntity>.TryCreateInstance();
            entity.Id = id;
            repository.Add(entity);
        }

        entity.IsPinned = isPinned;
        await EnforceSinglePinAsync(repository, [entity]);

        await repository.UnitOfWork.CommitAsync();

        SalesRepDocumentCacheRegion.ExpireRegion();
    }

    // At most one document is pinned: pinning one unpins every other, all within the same commit.
    protected virtual async Task EnforceSinglePinAsync(ISalesRepRepository repository, IList<DocumentMetadataEntity> savedEntities)
    {
        var winner = savedEntities.LastOrDefault(x => x.IsPinned);

        if (winner == null)
        {
            return;
        }

        // The DB query returns tracked instances for rows already loaded, so patched entities are covered too.
        var pinnedInDb = await repository.DocumentMetadata.Where(x => x.IsPinned).ToListAsync();

        foreach (var entity in pinnedInDb.Concat(savedEntities).Where(x => x.Id != winner.Id))
        {
            entity.IsPinned = false;
        }
    }

    public virtual async Task DeleteAsync(IList<string> ids)
    {
        if (ids.IsNullOrEmpty())
        {
            return;
        }

        using var repository = _repositoryFactory();
        var entities = await repository.GetDocumentMetadataByIdsAsync(ids);

        foreach (var entity in entities)
        {
            repository.Remove(entity);
        }

        await repository.UnitOfWork.CommitAsync();

        SalesRepDocumentCacheRegion.ExpireRegion();
    }
}
