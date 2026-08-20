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
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(metadataItems);

        var filesById = files
            .Where(x => x != null && ModuleConstants.DocumentsScope.EqualsIgnoreCase(x.Scope))
            .ToDictionary(x => x.Id);

        var documents = new List<SalesRepDocument>();
        foreach (var metadata in metadataItems)
        {
            if (!string.IsNullOrEmpty(metadata.FileId) && filesById.TryGetValue(metadata.FileId, out var file))
            {
                documents.Add(ToDocument(file, metadata));
            }
        }

        return documents;
    }
}
