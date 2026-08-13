using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepDocument : AuditableEntity
{
    public string Name { get; set; }

    public string Category { get; set; }

    public string ContentType { get; set; }

    public long Size { get; set; }

    // Download URL of the module's authorized endpoint — never the raw blob URL.
    public string Url { get; set; }

    public string Summary { get; set; }

    public int? PageCount { get; set; }

    public string PreviewUrl { get; set; }
}
