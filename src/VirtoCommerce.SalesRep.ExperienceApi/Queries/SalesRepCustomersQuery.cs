using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Index;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Query for the customer organizations the current Sales Rep is responsible for (VCST-5304).
/// The Sales Rep is the caller; their security account id is set server-side from the caller's claims.
/// </summary>
public class SalesRepCustomersQuery : SearchQuery<SalesRepCustomerSearchResult>, IHasIncludeFields
{
    /// <summary>Security account id of the current Sales Rep (set server-side from the caller's claims).</summary>
    public string UserId { get; set; }

    /// <summary>Optional store to scope each customer's <c>lastOrder</c> to (the storefront's current store).</summary>
    public string StoreId { get; set; }

    /// <summary>
    /// Optional customer-segment name (a <c>SalesRepCustomerFilterRule.name</c>) to narrow the list to; omit for all
    /// served customers. An unrecognized name yields no customers (fail-closed). Uses the module-wide
    /// <see cref="SalesRepFilters.ArgumentName"/> so every Sales Rep query selects a rule the same way.
    /// </summary>
    public string Filter { get; set; }

    /// <summary>
    /// Culture for the localized fields on each customer's <c>lastOrder</c> — <c>statusDisplayValue</c> and
    /// <c>total.formattedAmount</c> (e.g. "en-US"). Consumed by the <c>SalesRepOrderType</c> resolvers via the
    /// request context (the builder copies it to the UserContext), not by this handler.
    /// </summary>
    public string CultureName { get; set; }

    /// <summary>GraphQL selection paths of the requested fields — drives the member response group (load only what was asked for).</summary>
    public IList<string> IncludeFields { get; set; } = [];

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<StringGraphType>(nameof(StoreId), "Store to scope each customer's last order to (defaults to all stores).");
        yield return Argument<StringGraphType>(SalesRepFilters.ArgumentName, "Selected customer-segment name (a salesRepCustomerFilterRules 'name'); narrows to that segment. Omit for all served customers.");
        yield return Argument<StringGraphType>(nameof(CultureName), "Culture for each customer's lastOrder localized fields (statusDisplayValue, total.formattedAmount), e.g. \"en-US\".");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);
        UserId = context.GetCurrentUserId();
        StoreId = context.GetArgument<string>(nameof(StoreId));
        Filter = context.GetArgument<string>(SalesRepFilters.ArgumentName);
        CultureName = context.GetArgument<string>(nameof(CultureName));

        // Requested field paths (e.g. "items.address.city") → used to load only the needed member data.
        IncludeFields = context.SubFields?.Values.GetAllNodesPaths(context).ToArray() ?? [];
    }
}
