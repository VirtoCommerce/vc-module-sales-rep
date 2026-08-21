using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Models.Facets;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepMapper : ISalesRepMapper
{
    private const string TermAggregationType = "attr";
    private const string RangeAggregationType = "range";

    public virtual FacetResult ToFacet(OrderAggregation aggregation, string cultureName)
    {
        return aggregation?.AggregationType switch
        {
            TermAggregationType => ToTermFacet(aggregation, cultureName),
            RangeAggregationType => ToRangeFacet(aggregation),
            _ => null,
        };
    }

    public virtual IList<FacetResult> ToFacets(IList<OrderAggregation> aggregations, string cultureName)
    {
        return (aggregations ?? [])
            .Select(x => ToFacet(x, cultureName))
            .Where(x => x != null)
            .ToList();
    }

    protected virtual FacetResult ToTermFacet(OrderAggregation aggregation, string cultureName)
    {
        return new TermFacetResult
        {
            Name = aggregation.Field,
            Label = aggregation.Field,
            Terms = aggregation.Items?.Select(x => new FacetTerm
            {
                Term = x.Value?.ToString(),
                Label = GetLabel(x, cultureName) ?? x.Value?.ToString(),
                Count = x.Count,
                IsSelected = x.IsApplied,
            }).ToList() ?? [],
        };
    }

    protected virtual FacetResult ToRangeFacet(OrderAggregation aggregation)
    {
        return new RangeFacetResult
        {
            Name = aggregation.Field,
            Label = aggregation.Field,
            Ranges = aggregation.Items?.Select(x => new FacetRange
            {
                From = ToBound(x.RequestedLowerBound),
                IncludeFrom = x.IncludeLower,
                FromStr = x.RequestedLowerBound,
                To = ToBound(x.RequestedUpperBound),
                IncludeTo = x.IncludeUpper,
                ToStr = x.RequestedUpperBound,
                Label = x.Value?.ToString(),
                Count = x.Count,
                IsSelected = x.IsApplied,
            }).ToList() ?? [],
        };
    }

    protected virtual string GetLabel(OrderAggregationItem item, string cultureName)
    {
        return item.Labels?.FirstBestMatchForLanguage(x => x.Language, cultureName)?.Label;
    }

    // A range facet over a non-numeric field (a created-date window, say) has no numeric bound; the string
    // form still carries it, so the numeric one stays null instead of failing the whole response.
    protected static decimal? ToBound(string bound)
    {
        return decimal.TryParse(bound, NumberStyles.Any, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
