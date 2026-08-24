using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XOrder.Core;
using VirtoCommerce.XOrder.Core.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerOrderQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomerOrderQuery, CustomerOrderAggregate>
{
    private readonly ICustomerOrderAggregateRepository _orderAggregateRepository;
    private readonly ISalesRepOrderVisibilityService _orderVisibilityService;

    public SalesRepCustomerOrderQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ICustomerOrderAggregateRepository orderAggregateRepository,
        ISalesRepOrderVisibilityService orderVisibilityService)
        : base(organizationAccessService)
    {
        _orderAggregateRepository = orderAggregateRepository;
        _orderVisibilityService = orderVisibilityService;
    }

    public virtual async Task<CustomerOrderAggregate> Handle(SalesRepCustomerOrderQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Id))
        {
            return null;
        }

        var aggregate = await _orderAggregateRepository.GetOrderByIdAsync(request.Id);

        // An order outside the rep's served organizations reads as "not found", the same way salesRepCustomer
        // answers for an organization the rep does not serve.
        return await _orderVisibilityService.IsVisibleAsync(request.UserId, aggregate?.Order)
            ? aggregate
            : null;
    }
}
