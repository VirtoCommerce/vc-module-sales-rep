using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepSearchTermType : ExtendableGraphType<SalesRepSearchTerm>
{
    public SalesRepSearchTermType()
    {
        Name = "SalesRepSearchTerm";

        Field(x => x.Term, nullable: false).Description("The searched phrase.");
        Field(x => x.Count, nullable: false).Description("Number of tracked searches for the phrase in the period.");
        Field(x => x.LastSearchedDate, nullable: true).Description("Latest tracked search (UTC hour-bucket start); null under sort 'count' — the aggregate rows carry no dates.");
    }
}
