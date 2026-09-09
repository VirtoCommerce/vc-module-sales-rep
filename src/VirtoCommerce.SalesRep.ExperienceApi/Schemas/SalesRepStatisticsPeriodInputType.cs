using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepStatisticsPeriodInputType : InputObjectGraphType<SalesRepStatisticsPeriodInput>
{
    public SalesRepStatisticsPeriodInputType()
    {
        Name = "SalesRepStatisticsPeriodInput";

        // Shared by every period argument, and they do not all bound the same date - the argument the input is
        // passed to names the one it filters on.
        Field(x => x.From, nullable: true).Description("Inclusive lower bound of the period (null = no lower bound).");
        Field(x => x.To, nullable: true).Description("Inclusive upper bound of the period (null = no upper bound).");
    }
}
