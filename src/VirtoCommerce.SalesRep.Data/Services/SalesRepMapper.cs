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

    // Delegates so the facets match X-Order's own, including a project's own IXOrderMapper registration.
    public virtual IList<FacetResult> ToFacets(IList<OrderAggregation> aggregations, string cultureName)
    {
        return (aggregations ?? [])
            .Select(x => _orderMapper.ToFacetResult(x, cultureName))
            .Where(x => x != null)
            .ToList();
    }
}
