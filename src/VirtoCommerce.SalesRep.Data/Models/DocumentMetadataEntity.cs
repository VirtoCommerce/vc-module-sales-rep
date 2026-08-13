using System;
using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Data.Models;

// Id = the document's AssetEntry id (shared primary key, no FK — the AssetEntry table belongs to the Assets module).
public class DocumentMetadataEntity : AuditableEntity, IDataEntity<DocumentMetadataEntity, SalesRepDocumentMetadata>
{
    [StringLength(2048)]
    public string Summary { get; set; }

    public int? PageCount { get; set; }

    [StringLength(2083)]
    public string PreviewUrl { get; set; }

    public virtual DocumentMetadataEntity FromModel(SalesRepDocumentMetadata model, PrimaryKeyResolvingMap pkMap)
    {
        ArgumentNullException.ThrowIfNull(model);

        pkMap.AddPair(model, this);

        Id = model.Id;
        Summary = model.Summary;
        PageCount = model.PageCount;
        PreviewUrl = model.PreviewUrl;

        return this;
    }

    public virtual SalesRepDocumentMetadata ToModel(SalesRepDocumentMetadata model)
    {
        ArgumentNullException.ThrowIfNull(model);

        model.Id = Id;
        model.CreatedBy = CreatedBy;
        model.CreatedDate = CreatedDate;
        model.ModifiedBy = ModifiedBy;
        model.ModifiedDate = ModifiedDate;
        model.Summary = Summary;
        model.PageCount = PageCount;
        model.PreviewUrl = PreviewUrl;

        return model;
    }

    public virtual void Patch(DocumentMetadataEntity target)
    {
        target.Summary = Summary;
        target.PageCount = PageCount;
        target.PreviewUrl = PreviewUrl;
    }
}
