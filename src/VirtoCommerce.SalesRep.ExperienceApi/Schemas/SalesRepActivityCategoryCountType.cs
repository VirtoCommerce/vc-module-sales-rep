using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepActivityCategoryCountType : ExtendableGraphType<SalesRepActivityCategoryCount>
{
    public SalesRepActivityCategoryCountType()
    {
        Name = "SalesRepActivityCategoryCount";

        Field(x => x.Category, nullable: false).Description("Activity category (orders, customers, searches, productViews, logins).");
        Field(x => x.Count, nullable: false).Description("Total number of activity rows in this category for the applied filters.");
    }
}
