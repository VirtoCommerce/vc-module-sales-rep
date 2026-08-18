namespace VirtoCommerce.SalesRep.Core.Models;

// Step 2 of the two-step upload: the file is first uploaded to the sales-rep-documents scope via the
// file-experience-api endpoint (POST /api/files/{scope}), then registered in the library with this request.
public class SalesRepDocumentCreateRequest
{
    public string FileId { get; set; }

    public string Category { get; set; }

    public string Name { get; set; }

    public string Summary { get; set; }

    public int? PageCount { get; set; }

    public string PreviewUrl { get; set; }
}
