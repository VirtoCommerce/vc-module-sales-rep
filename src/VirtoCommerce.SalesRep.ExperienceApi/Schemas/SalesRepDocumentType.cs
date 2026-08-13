using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepDocumentType : ExtendableGraphType<SalesRepDocument>
{
    public SalesRepDocumentType()
    {
        Name = "SalesRepDocument";

        Field(x => x.Id, nullable: false).Description("Document id.");
        Field(x => x.Name, nullable: false).Description("Original file name (also the download file name).");
        Field(x => x.Category, nullable: true).Description("Category — the first-level subfolder of the shared library.");
        Field(x => x.ContentType, nullable: true).Description("MIME content type of the file.");
        Field(x => x.Size, nullable: false).Description("File size in bytes.");
        Field(x => x.CreatedDate, nullable: false).Description("Upload date (the default sort key, newest first).");
        Field(x => x.ModifiedDate, nullable: true).Description("Last modification date.");
        Field(x => x.Url, nullable: false).Description("Authorized download URL (the module's endpoint — never a raw blob URL).");
        Field(x => x.Summary, nullable: true).Description("Optional short description.");
        Field(x => x.PageCount, nullable: true).Description("Optional page count.");
        Field(x => x.PreviewUrl, nullable: true).Description("Optional preview image URL for the card grid.");
    }
}
