using VirtoCommerce.FileExperienceApi.Core.Extensions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;

namespace VirtoCommerce.SalesRep.Data.Services;

internal static class SalesRepDocumentMapper
{
    public static SalesRepDocument ToModel(File file, SalesRepDocumentMetadata metadata)
    {
        var document = AbstractTypeFactory<SalesRepDocument>.TryCreateInstance();

        document.Id = metadata.Id;
        document.CreatedBy = metadata.CreatedBy;
        document.CreatedDate = metadata.CreatedDate;
        document.ModifiedBy = metadata.ModifiedBy;
        document.ModifiedDate = metadata.ModifiedDate;
        document.FileId = file.Id;
        document.Name = file.Name;
        document.ContentType = file.ContentType;
        document.Size = file.Size;
        document.Url = FileUploadServiceExtensions.GetPublicUrl(file.Id);
        document.Category = metadata.Category;
        document.IsPinned = metadata.IsPinned;
        document.Summary = metadata.Summary;
        document.PageCount = metadata.PageCount;
        document.PreviewUrl = metadata.PreviewUrl;
        document.DisplayName = string.IsNullOrEmpty(metadata.Name) ? file.Name : metadata.Name;

        return document;
    }
}
