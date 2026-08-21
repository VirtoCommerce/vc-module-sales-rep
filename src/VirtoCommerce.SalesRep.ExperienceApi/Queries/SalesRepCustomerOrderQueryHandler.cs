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

    public SalesRepCustomerOrderQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ICustomerOrderAggregateRepository orderAggregateRepository)
        : base(organizationAccessService)
    {
        _orderAggregateRepository = orderAggregateRepository;
    }

    public virtual async Task<CustomerOrderAggregate> Handle(SalesRepCustomerOrderQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.Id))
        {
            return null;
        }

        var aggregate = await _orderAggregateRepository.GetOrderByIdAsync(request.Id);

        var organizationId = aggregate?.Order?.OrganizationId;
        if (string.IsNullOrEmpty(organizationId))
        {
            return null;
        }

        // An order outside the rep's served organizations reads as "not found", the same way salesRepCustomer
        // answers for an organization the rep does not serve.
        return await OrganizationAccessService.ServesOrganizationAsync(request.UserId, organizationId)
            ? aggregate
            : null;
    }
}
