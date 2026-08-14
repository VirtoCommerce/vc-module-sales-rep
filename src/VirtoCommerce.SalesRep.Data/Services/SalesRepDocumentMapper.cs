using VirtoCommerce.AssetsModule.Core.Assets;
using VirtoCommerce.Platform.Core.Common;
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
        document.ContentType = entry.BlobInfo?.ContentType;
        document.Size = entry.BlobInfo?.Size ?? 0;
        document.Url = GetDownloadUrl(entry.Id);

        if (metadata != null)
        {
            document.Category = metadata.Category;
            document.IsPinned = metadata.IsPinned;
            document.Summary = metadata.Summary;
            document.PageCount = metadata.PageCount;
            document.PreviewUrl = metadata.PreviewUrl;
        }

        document.DisplayName = string.IsNullOrEmpty(metadata?.Name) ? document.Name : metadata.Name;

        return document;
    }

    public static string GetDownloadUrl(string documentId)
    {
        return $"/api/sales-rep/documents/{documentId}";
    }
}
