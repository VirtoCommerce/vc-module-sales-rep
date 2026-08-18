using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.FileExperienceApi.Core.Extensions;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;

namespace VirtoCommerce.SalesRep.Data.Services;

// Files enter the library in two steps: uploaded to the sales-rep-documents scope through the file-experience-api
// endpoint (POST /api/files/{scope}) first, then registered here — CreateAsync validates the uploaded file, creates
// the metadata row, and takes ownership of the file so the generic file surfaces treat it as a library document.
public class SalesRepDocumentService : ISalesRepDocumentService
{
    private readonly IFileUploadService _fileUploadService;
    private readonly ISalesRepDocumentMetadataService _metadataService;

    public SalesRepDocumentService(
        IFileUploadService fileUploadService,
        ISalesRepDocumentMetadataService metadataService)
    {
        _fileUploadService = fileUploadService;
        _metadataService = metadataService;
    }

    public virtual async Task<SalesRepDocument> CreateAsync(string fileId, string category, SalesRepDocumentMetadata metadata = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileId);

        var safeCategory = SalesRepDocumentCategoryValidator.Sanitize(category, required: true);

        var file = await GetLibraryFileAsync(fileId)
            ?? throw new InvalidOperationException($"File '{fileId}' was not found in the '{ModuleConstants.DocumentsScope}' scope.");

        if (!file.OwnerIsEmpty())
        {
            throw new InvalidOperationException($"File '{fileId}' already belongs to a library document.");
        }

        metadata ??= AbstractTypeFactory<SalesRepDocumentMetadata>.TryCreateInstance();
        metadata.Id = null;
        metadata.FileId = file.Id;
        metadata.Category = safeCategory;
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
            await TryRunAsync(() => _metadataService.DeleteAsync([metadata.Id]));
            throw;
        }

        return SalesRepDocumentMapper.ToModel(file, metadata);
    }

    public virtual async Task<SalesRepDocument> UpdateMetadataAsync(string id, SalesRepDocumentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var existing = (await _metadataService.GetAsync([id])).FirstOrDefault()
            ?? throw new KeyNotFoundException($"Document '{id}' was not found in the library.");

        var file = await GetLibraryFileAsync(existing.FileId)
            ?? throw new KeyNotFoundException($"Document '{id}' was not found in the library.");

        metadata.Id = id;
        // The file link is immutable, and pin state is exclusively SetPinnedAsync's concern —
        // a full-replace metadata PUT must not change either.
        metadata.FileId = existing.FileId;
        metadata.IsPinned = existing.IsPinned;

        await _metadataService.SaveChangesAsync([metadata]);

        return SalesRepDocumentMapper.ToModel(file, metadata);
    }

    public virtual async Task DeleteAsync(IList<string> ids)
    {
        if (ids.IsNullOrEmpty())
        {
            return;
        }

        var documents = await _metadataService.GetAsync(ids);

        if (documents.Count == 0)
        {
            return;
        }

        await _metadataService.DeleteAsync(documents.Select(x => x.Id).ToList());

        // Best-effort: an already-missing file/blob must not fail the delete.
        var fileIds = documents.Select(x => x.FileId).Where(x => !string.IsNullOrEmpty(x)).ToList();
        if (fileIds.Count > 0)
        {
            await TryRunAsync(() => _fileUploadService.DeleteAsync(fileIds));
        }
    }

    public virtual async Task<SalesRepDocument> GetAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var metadata = (await _metadataService.GetAsync([id])).FirstOrDefault();

        if (metadata == null)
        {
            return null;
        }

        var file = await GetLibraryFileAsync(metadata.FileId);

        return file == null ? null : SalesRepDocumentMapper.ToModel(file, metadata);
    }

    protected virtual async Task<File> GetLibraryFileAsync(string fileId)
    {
        if (string.IsNullOrEmpty(fileId))
        {
            return null;
        }

        var file = (await _fileUploadService.GetAsync([fileId])).FirstOrDefault();

        return file != null && ModuleConstants.DocumentsScope.EqualsIgnoreCase(file.Scope) ? file : null;
    }

    private static async Task TryRunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch
        {
            // Intentionally swallowed: cleanup is best-effort and must not mask the original outcome.
        }
    }
}
