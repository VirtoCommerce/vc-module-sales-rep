using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepDocumentMetadata : AuditableEntity, ICloneable
{
    // Id of the library file (file-experience-api File / AssetEntry) this metadata describes.
    public string FileId { get; set; }

    public string Name { get; set; }

    public string Category { get; set; }

    public bool IsPinned { get; set; }

    public string Summary { get; set; }

    public int? PageCount { get; set; }

    public string PreviewUrl { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
