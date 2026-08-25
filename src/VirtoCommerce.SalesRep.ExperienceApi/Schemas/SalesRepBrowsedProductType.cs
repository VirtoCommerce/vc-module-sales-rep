using GraphQL.Types;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepBrowsedProductType : ExtendableGraphType<SalesRepBrowsedProduct>
{
    public SalesRepBrowsedProductType()
    {
        Name = "SalesRepBrowsedProduct";

        Field<NonNullGraphType<StringGraphType>>("productId")
            .Description("Resolved product id, falling back to the tracked product code when the code no longer matches a product.")
            .Resolve(context => context.Source.ProductId ?? context.Source.Code);
        Field(x => x.Name, nullable: true).Description("Product name (resolved from the catalog, falling back to the tracked name).");
        Field("sku", x => x.Code, nullable: true).Description("Product code (SKU) as tracked by analytics.");
        Field(x => x.ImageUrl, nullable: true).Description("Product image URL (null when unresolved).");
        Field(x => x.Slug, nullable: true).Description("Product SEO slug for deep-linking (null when unresolved; resolving it requires a storeId).");
        Field(x => x.ViewCount, nullable: false).Description("Number of tracked views of the product in the period.");
        Field(x => x.LastViewedDate, nullable: true).Description("Latest tracked view (UTC hour-bucket start); null under sort 'count' — the aggregate rows carry no dates.");
    }
}
