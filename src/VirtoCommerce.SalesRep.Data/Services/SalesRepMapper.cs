using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.FileExperienceApi.Core.Extensions;
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

        return MapDocument(file, metadata);
    }

    // The metadata list is authoritative: every row maps to a document. File-derived fields enrich when the
    // library-scope file record is found and stay null when it is not (out-of-band corruption) — the document
    // keeps listing and stays deletable, only its download degrades.
    public virtual IList<SalesRepDocument> ToDocuments(IList<File> files, IList<SalesRepDocumentMetadata> metadataItems)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(metadataItems);

        var filesById = files
            .Where(x => x != null && ModuleConstants.DocumentsScope.EqualsIgnoreCase(x.Scope))
            .ToDictionary(x => x.Id);

        return metadataItems
            .Select(metadata => MapDocument(
                !string.IsNullOrEmpty(metadata.FileId) && filesById.TryGetValue(metadata.FileId, out var file) ? file : null,
                metadata))
            .ToList();
    }

    protected virtual SalesRepDocument MapDocument(File file, SalesRepDocumentMetadata metadata)
    {
        var document = AbstractTypeFactory<SalesRepDocument>.TryCreateInstance();

        document.Id = metadata.Id;
        document.CreatedBy = metadata.CreatedBy;
        document.CreatedDate = metadata.CreatedDate;
        document.ModifiedBy = metadata.ModifiedBy;
        document.ModifiedDate = metadata.ModifiedDate;
        document.FileId = metadata.FileId;
        document.Category = metadata.Category;
        document.IsPinned = metadata.IsPinned;
        document.Summary = metadata.Summary;
        document.PageCount = metadata.PageCount;
        document.PreviewUrl = metadata.PreviewUrl;
        document.DisplayName = string.IsNullOrEmpty(metadata.Name) ? file?.Name : metadata.Name;
        // The download URL is deterministic, so it stays resolvable even for a degraded row — the download
        // then fails with the server's 404, the same way every other corruption class fails.
        document.Url = file?.PublicUrl ?? FileUploadServiceExtensions.GetPublicUrl(metadata.FileId);

        if (file != null)
        {
            document.Name = file.Name;
            document.ContentType = file.ContentType;
            document.Size = file.Size;
        }

        return document;
    }
}
