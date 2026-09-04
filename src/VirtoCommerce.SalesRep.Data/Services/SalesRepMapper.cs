using System;
using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.FileExperienceApi.Core.Extensions;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XOrder.Data.Services;
using File = VirtoCommerce.FileExperienceApi.Core.Models.File;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepMapper : ISalesRepMapper
{
    private readonly IXOrderMapper _orderMapper;

    public SalesRepMapper(IXOrderMapper orderMapper)
    {
        _orderMapper = orderMapper;
    }

    // Delegates so the facets match X-Order's own, including a project's own IXOrderMapper registration.
    public virtual IList<FacetResult> ToFacets(IList<OrderAggregation> aggregations, string cultureName)
    {
        return (aggregations ?? [])
            .Select(x => _orderMapper.ToFacetResult(x, cultureName))
            .Where(x => x != null)
            .ToList();
    }

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

    public virtual SalesRepDocumentMetadata ToMetadata(SalesRepDocument document)
    {
        if (document == null)
        {
            return null;
        }

        var metadata = AbstractTypeFactory<SalesRepDocumentMetadata>.TryCreateInstance();

        metadata.Name = document.DisplayName;
        metadata.Category = document.Category;
        metadata.Summary = document.Summary;
        metadata.PageCount = document.PageCount;
        metadata.PreviewUrl = document.PreviewUrl;

        return metadata;
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
