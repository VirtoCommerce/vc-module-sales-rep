using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

// Id = the document's AssetEntry id.
public class SalesRepDocumentMetadata : AuditableEntity, ICloneable
{
    // Optional display name shown in UIs instead of the raw file name.
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
