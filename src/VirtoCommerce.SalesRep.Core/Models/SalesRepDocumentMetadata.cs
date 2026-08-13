using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.SalesRep.Core.Models;

// Id = the document's AssetEntry id.
public class SalesRepDocumentMetadata : AuditableEntity
{
    public string Summary { get; set; }

    public int? PageCount { get; set; }

    public string PreviewUrl { get; set; }
}
