using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.FileExperienceApi.Core.Extensions;
using VirtoCommerce.FileExperienceApi.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;

namespace VirtoCommerce.SalesRep.Data.Services;

internal static class SalesRepDocumentMapper
{
    // Batch-fetches the metadata rows' files (library scope only) and maps the pairs; rows whose file is gone are skipped.
    public static async Task<IList<SalesRepDocument>> ToModelsAsync(IFileUploadService fileUploadService, IList<SalesRepDocumentMetadata> metadataItems)
    {
        if (metadataItems.Count == 0)
        {
            return [];
        }

        var filesById = (await fileUploadService.GetAsync(metadataItems.Select(x => x.FileId).ToList(), clone: false))
            .Where(x => ModuleConstants.DocumentsScope.EqualsIgnoreCase(x.Scope))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        return metadataItems
            .Where(x => filesById.ContainsKey(x.FileId))
            .Select(x => ToModel(filesById[x.FileId], x))
            .ToList();
    }

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
