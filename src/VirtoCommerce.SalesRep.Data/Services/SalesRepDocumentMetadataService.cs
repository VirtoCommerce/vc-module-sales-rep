using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
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

        using var repository = _repositoryFactory();
        var ids = metadata.Select(x => x.Id).ToList();
        var existingEntities = await repository.GetDocumentMetadataByIdsAsync(ids);
        var pkMap = new PrimaryKeyResolvingMap();

        foreach (var model in metadata)
        {
            var sourceEntity = AbstractTypeFactory<DocumentMetadataEntity>.TryCreateInstance().FromModel(model, pkMap);
            var targetEntity = existingEntities.FirstOrDefault(x => x.Id == model.Id);

            if (targetEntity != null)
            {
                sourceEntity.Patch(targetEntity);
            }
            else
            {
                repository.Add(sourceEntity);
            }
        }

        await repository.UnitOfWork.CommitAsync();
        pkMap.ResolvePrimaryKeys();
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
    }
}
