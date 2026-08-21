using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XOrder.Data.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

public class SalesRepMapper : ISalesRepMapper
{
    private readonly IXOrderMapper _orderMapper;

    public SalesRepMapper(IXOrderMapper orderMapper)
    {
        _orderMapper = orderMapper;
    }

    // Delegates rather than reimplements, so the facets this module returns keep the shape X-Order's own order
    // queries return - including whatever a project registered in place of IXOrderMapper.
    public virtual IList<FacetResult> ToFacets(IList<OrderAggregation> aggregations, string cultureName)
    {
        return (aggregations ?? [])
            .Select(x => _orderMapper.ToFacetResult(x, cultureName))
            .Where(x => x != null)
            .ToList();
    }
}
