using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepStatisticsPeriodInputType : InputObjectGraphType<SalesRepStatisticsPeriodInput>
{
    public SalesRepStatisticsPeriodInputType()
    {
        Name = "SalesRepStatisticsPeriodInput";

        Field(x => x.From, nullable: true).Description("Inclusive lower bound on the created date (null = no lower bound).");
        Field(x => x.To, nullable: true).Description("Inclusive upper bound on the created date (null = no upper bound).");
    }
}
