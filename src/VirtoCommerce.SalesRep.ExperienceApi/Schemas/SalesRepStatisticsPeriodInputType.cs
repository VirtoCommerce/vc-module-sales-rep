using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

/// <summary>Shared date-range input for the statistics <c>comparison</c> fields (orders, carts, customers).</summary>
public class SalesRepStatisticsPeriodInputType : InputObjectGraphType<SalesRepStatisticsPeriodInput>
{
    public SalesRepStatisticsPeriodInputType()
    {
        Name = "SalesRepStatisticsPeriodInput";

        Field(x => x.From, nullable: true).Description("Inclusive lower bound on the created date (null = no lower bound).");
        Field(x => x.To, nullable: true).Description("Exclusive upper bound on the created date (null = no upper bound).");
    }
}
