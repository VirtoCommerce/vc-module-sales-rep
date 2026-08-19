namespace VirtoCommerce.SalesRep.Core.Models;

public class SalesRepDocumentCreateRequest
{
    public string FileId { get; set; }

    public string Category { get; set; }

    public string Name { get; set; }

    public string Summary { get; set; }

    public int? PageCount { get; set; }

    public string PreviewUrl { get; set; }
}
