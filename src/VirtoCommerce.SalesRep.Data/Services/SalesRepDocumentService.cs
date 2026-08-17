using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.AssetsModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Data.Caching;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepDocumentService : ISalesRepDocumentService
{
    private const int RandomSuffixLength = 8;
    private const int MaxSlugLength = 64;

    private static readonly char[] PathSeparators = ['/', '\\'];

    private readonly IBlobStorageProvider _blobStorageProvider;
    private readonly IAssetEntryService _assetEntryService;
    private readonly ISalesRepDocumentMetadataService _metadataService;
    private readonly IFileExtensionService _fileExtensionService;

    public SalesRepDocumentService(
        IBlobStorageProvider blobStorageProvider,
        IAssetEntryService assetEntryService,
        ISalesRepDocumentMetadataService metadataService,
        IFileExtensionService fileExtensionService)
    {
        _blobStorageProvider = blobStorageProvider;
        _assetEntryService = assetEntryService;
        _metadataService = metadataService;
        _fileExtensionService = fileExtensionService;
    }

    protected virtual long MaxFileSize => ModuleConstants.Documents.MaxFileSize;

    public virtual async Task<SalesRepDocument> UploadAsync(Stream stream, string fileName, string category, SalesRepDocumentMetadata metadata = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var safeCategory = SalesRepDocumentCategoryValidator.Sanitize(category, required: true);

        // Strip client-supplied path components on every OS (Path.GetFileName only adds Windows
        // volume-separator handling, irrelevant for an upload filename).
        var safeName = fileName.Trim();
        safeName = safeName[(safeName.LastIndexOfAny(PathSeparators) + 1)..];

        if (!await _fileExtensionService.IsExtensionAllowedAsync(safeName))
        {
            throw new InvalidOperationException($"File extension '{Path.GetExtension(safeName)}' is not allowed.");
        }

        if (stream.CanSeek && stream.Length > MaxFileSize)
        {
            throw new InvalidOperationException($"File size exceeds the {MaxFileSize} bytes limit.");
        }

        // Blobs are stored flat under the library root; the category lives in the metadata row.
        var blobUrl = $"{ModuleConstants.DocumentsScope}/{BuildBlobName(safeName)}";
        var blobWritten = false;
        AssetEntry entry = null;

        try
        {
            long size;
            await using (var targetStream = await _blobStorageProvider.OpenWriteAsync(blobUrl))
            {
                blobWritten = true;
                size = await CopyBoundedAsync(stream, targetStream);
            }

            entry = AbstractTypeFactory<AssetEntry>.TryCreateInstance();
            entry.Id = Guid.NewGuid().ToString("N");
            entry.Group = ModuleConstants.DocumentsScope;
            entry.BlobInfo = AbstractTypeFactory<BlobInfo>.TryCreateInstance();
            entry.BlobInfo.Name = safeName;
            entry.BlobInfo.RelativeUrl = blobUrl;
            entry.BlobInfo.ContentType = MimeTypeResolver.ResolveContentType(safeName);
            entry.BlobInfo.Size = size;

            await _assetEntryService.SaveChangesAsync([entry]);

            metadata ??= AbstractTypeFactory<SalesRepDocumentMetadata>.TryCreateInstance();
            metadata.Id = entry.Id;
            metadata.Category = safeCategory;
            await _metadataService.CreateAsync([metadata]);

            SalesRepDocumentCacheRegion.ExpireRegion();

            return SalesRepDocumentMapper.ToModel(entry, metadata);
        }
        catch
        {
            // Best-effort rollback so a failed upload leaves no orphan blob/entry behind.
            if (entry?.Id != null)
            {
                await TryRunAsync(() => _assetEntryService.DeleteAsync([entry.Id]));
            }

            if (blobWritten)
            {
                await TryRunAsync(() => _blobStorageProvider.RemoveAsync([blobUrl]));
            }

            throw;
        }
    }

    public virtual async Task DeleteAsync(IList<string> ids)
    {
        if (ids.IsNullOrEmpty())
        {
            return;
        }

        // Only Id and BlobInfo.RelativeUrl are read below, so skip the defensive deep-copy.
        var entries = await _assetEntryService.GetAsync(ids, clone: false);
        var documents = entries.Where(IsLibraryEntry).ToList();

        if (documents.Count == 0)
        {
            return;
        }

        var documentIds = documents.Select(x => x.Id).ToList();
        await _metadataService.DeleteAsync(documentIds);
        await _assetEntryService.DeleteAsync(documentIds);

        // Each blob failure is tolerated independently (an already-missing blob must not fail the delete);
        // the removals are best-effort and run concurrently.
        var removals = documents
            .Select(x => x.BlobInfo?.RelativeUrl)
            .Where(x => !string.IsNullOrEmpty(x))
            .Select(blobUrl => TryRunAsync(() => _blobStorageProvider.RemoveAsync([blobUrl])));

        await Task.WhenAll(removals);

        SalesRepDocumentCacheRegion.ExpireRegion();
    }

    public virtual async Task<SalesRepDocument> GetAsync(string id)
    {
        var entry = await GetLibraryEntryAsync(id);

        if (entry == null)
        {
            return null;
        }

        var metadata = (await _metadataService.GetByIdsAsync([id])).FirstOrDefault();

        return SalesRepDocumentMapper.ToModel(entry, metadata);
    }

    public virtual async Task<Stream> OpenReadAsync(string id)
    {
        var entry = await GetLibraryEntryAsync(id);

        return entry?.BlobInfo?.RelativeUrl is null
            ? null
            : await _blobStorageProvider.OpenReadAsync(entry.BlobInfo.RelativeUrl);
    }

    protected virtual async Task<AssetEntry> GetLibraryEntryAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var entry = await _assetEntryService.GetNoCloneAsync(id);

        return entry != null && IsLibraryEntry(entry) ? entry : null;
    }

    protected static bool IsLibraryEntry(AssetEntry entry)
    {
        return ModuleConstants.DocumentsScope.EqualsIgnoreCase(entry.Group);
    }

    // "{slug}-{8charrandom}{ext}": randomized for collision handling + defense-in-depth; the human
    // name stays in AssetEntry.Name and is used as the download filename.
    protected static string BuildBlobName(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var slug = Regex.Replace(Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

        if (slug.Length > MaxSlugLength)
        {
            slug = slug[..MaxSlugLength].Trim('-');
        }

        if (slug.Length == 0)
        {
            slug = "document";
        }

        var random = Guid.NewGuid().ToString("N")[..RandomSuffixLength];

        return $"{slug}-{random}{extension}";
    }

    protected virtual async Task<long> CopyBoundedAsync(Stream source, Stream target)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;

        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            total += read;

            if (total > MaxFileSize)
            {
                throw new InvalidOperationException($"File size exceeds the {MaxFileSize} bytes limit.");
            }

            await target.WriteAsync(buffer.AsMemory(0, read));
        }

        return total;
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
