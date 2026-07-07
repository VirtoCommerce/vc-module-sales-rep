using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Query for the details of a single customer organization the current Sales Rep is responsible for (VCST-5308).
/// The Sales Rep is the caller; their security account id is set server-side from the caller's claims and the
/// handler verifies the caller actually serves the requested organization.
/// </summary>
public class SalesRepCustomerQuery : Query<SalesRepCustomerDetails>
{
    /// <summary>Organization (customer) id to load.</summary>
    public string OrganizationId { get; set; }

    /// <summary>Security account id of the current Sales Rep (set server-side from the caller's claims).</summary>
    public string UserId { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        yield return Argument<NonNullGraphType<StringGraphType>>("id", "Organization (customer) id.");
    }

    public override void Map(IResolveFieldContext context)
    {
        // Identity comes from the caller's claims; only the organization id is a client argument.
        OrganizationId = context.GetArgument<string>("id");
        UserId = context.GetCurrentUserId();
    }
}
