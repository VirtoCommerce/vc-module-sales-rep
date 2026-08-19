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
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;

namespace VirtoCommerce.SalesRep.Data.Services;

// Two-step intake: files are uploaded to the sales-rep-documents scope via file-experience-api first; CreateAsync registers (claims) them.
public class SalesRepDocumentService : ISalesRepDocumentService
{
    private readonly IFileUploadService _fileUploadService;
    private readonly ISalesRepDocumentMetadataService _metadataService;
    private readonly ILogger<SalesRepDocumentService> _logger;

    public SalesRepDocumentService(
        IFileUploadService fileUploadService,
        ISalesRepDocumentMetadataService metadataService,
        ILogger<SalesRepDocumentService> logger)
    {
        _fileUploadService = fileUploadService;
        _metadataService = metadataService;
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
        // The display name is the search surface, so it is always stored.
        metadata.Name = NormalizeName(metadata.Name, file.Name);
        metadata.Category = category;
        metadata.IsPinned = false;

        await _metadataService.SaveChangesAsync([metadata]);

        try
        {
            file.OwnerEntityId = metadata.Id;
            file.OwnerEntityType = nameof(SalesRepDocumentMetadata);
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

        return SalesRepDocumentMapper.ToModel(file, metadata);
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
        // The file link is immutable, and pin state is exclusively SetPinnedAsync's concern —
        // a full-replace metadata PUT must not change either.
        metadata.FileId = existing.FileId;
        metadata.IsPinned = existing.IsPinned;
        metadata.Name = NormalizeName(metadata.Name, file.Name);

        await _metadataService.SaveChangesAsync([metadata]);

        // The audit stamps land only on the stored row; re-read so the response carries them.
        var saved = await _metadataService.GetNoCloneAsync(id);

        return SalesRepDocumentMapper.ToModel(file, saved);
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
        if (ids.IsNullOrEmpty())
        {
            return;
        }

        var documents = await _metadataService.GetNoCloneAsync(ids);

        if (documents.Count == 0)
        {
            return;
        }

        // Files first: if the file store fails, the documents stay listed and the delete stays retryable — the
        // reverse order would leave readable files unreachable through the module. One file per call so a blob
        // already missing from the physical storage (deleting it was the goal) doesn't abort the rest.
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

        return await SalesRepDocumentMapper.ToModelsAsync(_fileUploadService, metadataItems);
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
