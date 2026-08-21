using System.Collections.Generic;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Xapi.Core.Models.Facets;

namespace VirtoCommerce.SalesRep.Core.Services;

public interface ISalesRepMapper
{
    IList<FacetResult> ToFacets(IList<OrderAggregation> aggregations, string cultureName);
}
