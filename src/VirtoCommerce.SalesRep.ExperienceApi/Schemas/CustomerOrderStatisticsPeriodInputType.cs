using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class CustomerOrderStatisticsPeriodInputType : InputObjectGraphType<CustomerOrderStatisticsPeriodInput>
{
    public CustomerOrderStatisticsPeriodInputType()
    {
        Name = "CustomerOrderStatisticsPeriodInput";

        Field(x => x.From, nullable: true).Description("Inclusive lower bound on the order created date (null = no lower bound).");
        Field(x => x.To, nullable: true).Description("Exclusive upper bound on the order created date (null = no upper bound).");
    }
}
