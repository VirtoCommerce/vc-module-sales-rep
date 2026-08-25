using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepActivityProductType : ExtendableGraphType<SalesRepActivityProduct>
{
    public SalesRepActivityProductType()
    {
        Name = "SalesRepActivityProduct";

        Field(x => x.Code, nullable: false).Description("Product code as tracked by analytics; always present, even when the product could not be resolved.");
        Field(x => x.ProductId, nullable: true).Description("Resolved product id (null when the code no longer matches a product).");
        Field(x => x.Name, nullable: true).Description("Product name (resolved from the catalog, falling back to the tracked name).");
        Field(x => x.Slug, nullable: true).Description("Product SEO slug for deep-linking (null when unresolved; resolving it requires a storeId).");
        Field(x => x.ImageUrl, nullable: true).Description("Product image URL (null when unresolved).");
    }
}
