using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepDocumentCategoryType : ExtendableGraphType<SalesRepDocumentCategory>
{
    public SalesRepDocumentCategoryType()
    {
        Name = "SalesRepDocumentCategory";

        Field(x => x.Name, nullable: false).Description("Category name — send it back in the salesRepDocuments 'category' argument.");
        Field(x => x.Count, nullable: false).Description("Number of documents in the category.");
    }
}
