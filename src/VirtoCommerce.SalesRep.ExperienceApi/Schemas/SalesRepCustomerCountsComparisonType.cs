using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCustomerCountsComparisonType : ExtendableGraphType<SalesRepCustomerCountsComparison>
{
    public SalesRepCustomerCountsComparisonType()
    {
        Name = "SalesRepCustomerCountsComparison";

        Field(x => x.OrderingCustomersChange, nullable: false).Description("Current ordering-customers count minus previous.");
        Field(x => x.OrderingCustomersChangePercent, nullable: true).Description("Percentage change of ordering-customers; null when the previous count is zero.");
        Field(x => x.NewCustomersChange, nullable: false).Description("Current new-customers count minus previous.");
        Field(x => x.NewCustomersChangePercent, nullable: true).Description("Percentage change of new-customers; null when the previous count is zero.");
    }
}
