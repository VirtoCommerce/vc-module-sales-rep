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

        var file = await GetLibraryFileAsync(fileId)
            ?? throw new InvalidOperationException($"File '{fileId}' was not found in the '{ModuleConstants.DocumentsScope}' scope.");

        if (!file.OwnerIsEmpty())
        {
            throw new InvalidOperationException($"File '{fileId}' already belongs to a library document.");
        }

        metadata ??= AbstractTypeFactory<SalesRepDocumentMetadata>.TryCreateInstance();
        metadata.Id = null;
        metadata.FileId = file.Id;
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

        // DisplayName is the metadata name coalesced with the file name, so a value equal to the file name
        // is stored as "no override" — the read side keeps falling back to the file name.
        metadata.Name = document.DisplayName != document.Name ? document.DisplayName : null;
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

    public virtual async Task<IList<SalesRepDocument>> GetAsync(IList<string> ids, string responseGroup = null, bool clone = true)
    {
        if (ids.IsNullOrEmpty())
        {
            return [];
        }

        var metadatas = await _metadataService.GetAsync(ids, responseGroup);

        if (metadatas.Count == 0)
        {
            return [];
        }

        var filesById = (await _fileUploadService.GetAsync(metadatas.Select(x => x.FileId).ToList()))
            .Where(x => ModuleConstants.DocumentsScope.EqualsIgnoreCase(x.Scope))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        return metadatas
            .Where(x => filesById.ContainsKey(x.FileId))
            .Select(x => SalesRepDocumentMapper.ToModel(filesById[x.FileId], x))
            .ToList();
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
