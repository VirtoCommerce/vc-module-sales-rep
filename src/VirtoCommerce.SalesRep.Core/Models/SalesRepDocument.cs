using System;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepDocument : AuditableEntity, ICloneable
{
    // Raw uploaded file name (also the download file name).
    public string Name { get; set; }

    // Metadata name when set, otherwise the file name.
    public string DisplayName { get; set; }

    public string Category { get; set; }

    public bool IsPinned { get; set; }

    public string ContentType { get; set; }

    public long Size { get; set; }

    // Download URL of the module's authorized endpoint — never the raw blob URL.
    public string Url { get; set; }

    public string Summary { get; set; }

    public int? PageCount { get; set; }

    public string PreviewUrl { get; set; }

    public virtual object Clone()
    {
        return MemberwiseClone();
    }
}
