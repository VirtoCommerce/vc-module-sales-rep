using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VirtoCommerce.FileExperienceApi.Core.Extensions;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Extensions;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;

namespace VirtoCommerce.SalesRep.Data.Services;

// Two-step intake: files are uploaded to the sales-rep-documents scope via file-experience-api first; CreateAsync registers (claims) them.
public class SalesRepDocumentService : ISalesRepDocumentService
{
    private readonly IFileUploadService _fileUploadService;
    private readonly ISalesRepDocumentMetadataService _metadataService;
    private readonly ISalesRepMapper _mapper;
    private readonly ILogger<SalesRepDocumentService> _logger;

    public SalesRepDocumentService(
        IFileUploadService fileUploadService,
        ISalesRepDocumentMetadataService metadataService,
        ISalesRepMapper mapper,
        ILogger<SalesRepDocumentService> logger)
    {
        _fileUploadService = fileUploadService;
        _metadataService = metadataService;
        _mapper = mapper;
        _logger = logger;
    }

    public virtual async Task<SalesRepDocument> CreateAsync(string fileId, string category, SalesRepDocumentMetadata metadata = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileId);

        var file = await GetLibraryFileAsync(fileId)
            ?? throw new InvalidOperationException($"File '{fileId}' was not found in the '{ModuleConstants.DocumentsScope}' scope.");

        if (!file.OwnerIsEmpty())
        {
            throw new InvalidOperationException($"File '{fileId}' already belongs to a library document.");
        }

        if (file.Size == 0)
        {
            throw new InvalidOperationException($"File '{fileId}' is empty.");
        }

        metadata ??= AbstractTypeFactory<SalesRepDocumentMetadata>.TryCreateInstance();
        metadata.Id = null;
        metadata.FileId = file.Id;
        // The display name is the search surface, so it is always stored. Pin state needs no reset: the entity
        // never reads IsPinned from a saved model — the column is written only by SetPinnedAsync.
        metadata.Name = NormalizeName(metadata.Name, file.Name);
        metadata.Category = category;

        await _metadataService.SaveChangesAsync([metadata]);

        try
        {
            file.SetOwner(metadata);
            await _fileUploadService.SaveChangesAsync([file]);
        }
        catch
        {
            // Best-effort rollback so a failed claim leaves no metadata row behind.
            try
            {
                await _metadataService.DeleteAsync([metadata.Id]);
            }
            catch (Exception cleanupException)
            {
                _logger.LogWarning(cleanupException, "Failed to roll back metadata '{MetadataId}' after a failed claim of file '{FileId}'.", metadata.Id, fileId);
            }

            throw;
        }

        return _mapper.ToDocument(file, metadata);
    }

    public virtual async Task<SalesRepDocument> UpdateMetadataAsync(string id, SalesRepDocumentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var existing = await _metadataService.GetNoCloneAsync(id);
        if (existing == null)
        {
            return null;
        }

        var file = await GetLibraryFileAsync(existing.FileId);
        if (file == null)
        {
            return null;
        }

        metadata.Id = id;
        // The file link is immutable — a full-replace metadata PUT must not change it. (Pin state is already
        // untouchable here: the entity never reads IsPinned from a saved model.)
        metadata.FileId = existing.FileId;
        metadata.Name = NormalizeName(metadata.Name, file.Name);

        await _metadataService.SaveChangesAsync([metadata]);

        // IMPORTANT (keep): not redundant — SaveChangesAsync never syncs a non-transient input model. The platform's
        // CrudService.ToModel(entity, model) ignores `model` (rebuilds the changed entry from the entity), and
        // PrimaryKeyResolvingMap syncs id/audit back only for transient models. Re-read so the response carries
        // the stored audit stamps (create needs no re-read for the same reason).
        var saved = await _metadataService.GetNoCloneAsync(id);

        return _mapper.ToDocument(file, saved);
    }

    public virtual async Task SaveChangesAsync(IList<SalesRepDocument> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        foreach (var model in models)
        {
            await SaveOneAsync(model);
        }
    }

    protected virtual async Task SaveOneAsync(SalesRepDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var metadata = ToMetadata(document);

        if (string.IsNullOrEmpty(document.Id))
        {
            var created = await CreateAsync(document.FileId, document.Category, metadata);
            document.Id = created.Id;
        }
        else
        {
            await UpdateMetadataAsync(document.Id, metadata);
        }
    }

    protected virtual SalesRepDocumentMetadata ToMetadata(SalesRepDocument document)
    {
        var metadata = AbstractTypeFactory<SalesRepDocumentMetadata>.TryCreateInstance();

        metadata.Name = document.DisplayName;
        metadata.Category = document.Category;
        metadata.Summary = document.Summary;
        metadata.PageCount = document.PageCount;
        metadata.PreviewUrl = document.PreviewUrl;

        return metadata;
    }

    public virtual async Task DeleteAsync(IList<string> ids, bool softDelete = false)
    {
        if (softDelete)
        {
            throw new NotSupportedException("Soft delete is not supported: a document delete is always permanent.");
        }

        if (ids.IsNullOrEmpty())
        {
            return;
        }

        var documents = await _metadataService.GetNoCloneAsync(ids);

        if (documents.Count == 0)
        {
            return;
        }

        // The delete CONVERGES rather than promising ordering: the file service removes the file record before
        // the blob and its record deletion cascades into the metadata (AssetEntryChangedEvent) mid-call, so a
        // propagated storage failure could not keep the document anyway. File-store failures are logged and never
        // abort the batch; the metadata sweep below is the authoritative cleanup for whatever the cascade did not
        // already remove. Storage debris (a leaked blob, or a still-claimed file when the record delete itself
        // failed) is tolerated, as everywhere in the platform, and removable with the asset admin tools.
        foreach (var fileId in documents.Select(x => x.FileId).Where(x => !string.IsNullOrEmpty(x)))
        {
            try
            {
                await _fileUploadService.DeleteAsync([fileId]);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                _logger.LogWarning(ex, "File '{FileId}' is already missing from the storage; deleting the document anyway.", fileId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file '{FileId}'; deleting the document anyway.", fileId);
            }
        }

        await _metadataService.DeleteAsync(documents.Select(x => x.Id).ToList());
    }

    public virtual async Task<IList<SalesRepDocument>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        var metadataItems = await _metadataService.GetNoCloneAsync(ids, responseGroup);

        return await _mapper.ToDocumentsAsync(_fileUploadService, metadataItems);
    }

    protected virtual async Task<File> GetLibraryFileAsync(string fileId)
    {
        if (string.IsNullOrEmpty(fileId))
        {
            return null;
        }

        var file = await _fileUploadService.GetByIdAsync(fileId);

        return file != null && ModuleConstants.DocumentsScope.EqualsIgnoreCase(file.Scope) ? file : null;
    }

    private static string NormalizeName(string name, string fileName)
    {
        return string.IsNullOrWhiteSpace(name) ? fileName : name.Trim();
    }

}
