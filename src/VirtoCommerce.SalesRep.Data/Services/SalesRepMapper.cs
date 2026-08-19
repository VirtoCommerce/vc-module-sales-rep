using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepMapper : ISalesRepMapper
{
    public virtual SalesRepDocument ToDocument(File file, SalesRepDocumentMetadata metadata)
    {
        if (file == null || metadata == null)
        {
            return null;
        }

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
        document.Url = file.PublicUrl;
        document.Category = metadata.Category;
        document.IsPinned = metadata.IsPinned;
        document.Summary = metadata.Summary;
        document.PageCount = metadata.PageCount;
        document.PreviewUrl = metadata.PreviewUrl;
        document.DisplayName = string.IsNullOrEmpty(metadata.Name) ? file.Name : metadata.Name;

        return document;
    }

    public virtual IList<SalesRepDocument> ToDocuments(IList<File> files, IList<SalesRepDocumentMetadata> metadataItems)
    {
        if (files == null || metadataItems == null)
        {
            return null;
        }

        var filesById = files
            .Where(x => x != null && ModuleConstants.DocumentsScope.EqualsIgnoreCase(x.Scope))
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        return metadataItems
            .Where(x => filesById.ContainsKey(x.FileId))
            .Select(x => ToDocument(filesById[x.FileId], x))
            .ToList();
    }
}
