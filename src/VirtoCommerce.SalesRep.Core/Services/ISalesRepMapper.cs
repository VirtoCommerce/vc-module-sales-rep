using System.Collections.Generic;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Xapi.Core.Models.Facets;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepMapper
{
    FacetResult ToFacet(OrderAggregation aggregation, string cultureName);

    // Aggregations the order index returned alongside a list -> the facets its connection exposes.
    IList<FacetResult> ToFacets(IList<OrderAggregation> aggregations, string cultureName);
}
