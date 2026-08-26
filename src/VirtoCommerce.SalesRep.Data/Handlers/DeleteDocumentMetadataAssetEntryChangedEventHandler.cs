using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VirtoCommerce.AssetsModule.Core.Events;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Events;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Repositories;

namespace VirtoCommerce.SalesRep.Data.Handlers;

// Deleting a library file through any IAssetEntryService path (the generic deleteFile mutation, the platform
// asset admin APIs) must also drop the sidecar metadata row — otherwise it lingers as an invisible orphan that
// inflates the search TotalCount and blocks the unique FileId slot.
public class DeleteDocumentMetadataAssetEntryChangedEventHandler : IEventHandler<AssetEntryChangedEvent>
{
    private readonly Func<ISalesRepRepository> _repositoryFactory;
    private readonly ISalesRepDocumentMetadataService _metadataService;

    public DeleteDocumentMetadataAssetEntryChangedEventHandler(
        Func<ISalesRepRepository> repositoryFactory,
        ISalesRepDocumentMetadataService metadataService)
    {
        _repositoryFactory = repositoryFactory;
        _metadataService = metadataService;
    }

    public virtual async Task Handle(AssetEntryChangedEvent message)
    {
        var deletedFileIds = message.ChangedEntries
            .Where(x => x.EntryState == EntryState.Deleted)
            .Select(x => x.OldEntry ?? x.NewEntry)
            .Where(x => x != null && x.Group.EqualsIgnoreCase(ModuleConstants.DocumentsScope))
            .Select(x => x.Id)
            .ToArray();

        if (deletedFileIds.Length == 0)
        {
            return;
        }

        List<string> metadataIds;
        using (var repository = _repositoryFactory())
        {
            metadataIds = await repository.DocumentMetadata
                .Where(x => deletedFileIds.Contains(x.FileId))
                .Select(x => x.Id)
                .ToListAsync();
        }

        if (metadataIds.Count > 0)
        {
            await _metadataService.DeleteAsync(metadataIds);
        }
    }
}
