using System;
using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Data.Services;

internal static class SalesRepDocumentMapper
{
    public static SalesRepDocument ToModel(AssetEntry entry, SalesRepDocumentMetadata metadata)
    {
        var document = AbstractTypeFactory<SalesRepDocument>.TryCreateInstance();

        document.Id = entry.Id;
        document.CreatedBy = entry.CreatedBy;
        document.CreatedDate = entry.CreatedDate;
        document.ModifiedBy = entry.ModifiedBy;
        document.ModifiedDate = entry.ModifiedDate;
        document.Name = entry.BlobInfo?.Name;
        document.Category = GetCategory(entry.BlobInfo?.RelativeUrl);
        document.ContentType = entry.BlobInfo?.ContentType;
        document.Size = entry.BlobInfo?.Size ?? 0;
        document.Url = GetDownloadUrl(entry.Id);

        if (metadata != null)
        {
            document.Summary = metadata.Summary;
            document.PageCount = metadata.PageCount;
            document.PreviewUrl = metadata.PreviewUrl;
        }

        return document;
    }

    public static string GetDownloadUrl(string documentId)
    {
        return $"/api/sales-rep/documents/{documentId}";
    }

    // Category = first subfolder under the library root ("sales-rep-documents/{category}/{blobName}").
    public static string GetCategory(string relativeUrl)
    {
        var segments = relativeUrl?.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries) ?? [];

        return segments.Length >= 3 && segments[0].EqualsIgnoreCase(ModuleConstants.DocumentsScope)
            ? segments[1]
            : string.Empty;
    }
}
