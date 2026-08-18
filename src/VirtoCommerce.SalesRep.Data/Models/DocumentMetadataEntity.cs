using System;
using System.ComponentModel.DataAnnotations;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Domain;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;

namespace VirtoCommerce.SalesRep.Data.Models;

public class DocumentMetadataEntity : AuditableEntity, IDataEntity<DocumentMetadataEntity, SalesRepDocumentMetadata>
{
    public const int FileIdLength = 128;
    public const int NameLength = 512;
    public const int SummaryLength = 2048;
    public const int PreviewUrlLength = 2083;

    // The library file id (file-experience-api File / AssetEntry). No FK — the AssetEntry table belongs to the Assets module.
    [Required]
    [StringLength(FileIdLength)]
    public string FileId { get; set; }

    [StringLength(NameLength)]
    public string Name { get; set; }

    // The column length IS the business cap — one number, defined once.
    [Required]
    [StringLength(ModuleConstants.Documents.CategoryMaxLength)]
    public string Category { get; set; }

    public bool IsPinned { get; set; }

    [StringLength(SummaryLength)]
    public string Summary { get; set; }

    public int? PageCount { get; set; }

    [StringLength(PreviewUrlLength)]
    public string PreviewUrl { get; set; }

    public virtual DocumentMetadataEntity FromModel(SalesRepDocumentMetadata model, PrimaryKeyResolvingMap pkMap)
    {
        ArgumentNullException.ThrowIfNull(model);

        pkMap.AddPair(model, this);

        Id = model.Id;
        CreatedBy = model.CreatedBy;
        CreatedDate = model.CreatedDate;
        ModifiedBy = model.ModifiedBy;
        ModifiedDate = model.ModifiedDate;
        FileId = model.FileId;
        Name = model.Name;
        Category = model.Category;
        IsPinned = model.IsPinned;
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
        model.FileId = FileId;
        model.Name = Name;
        model.Category = Category;
        model.IsPinned = IsPinned;
        model.Summary = Summary;
        model.PageCount = PageCount;
        model.PreviewUrl = PreviewUrl;

        return model;
    }

    // FileId is deliberately not patched: the file link is immutable after the document is created.
    public virtual void Patch(DocumentMetadataEntity target)
    {
        target.Name = Name;
        target.Category = Category;
        target.IsPinned = IsPinned;
        target.Summary = Summary;
        target.PageCount = PageCount;
        target.PreviewUrl = PreviewUrl;
    }
}
