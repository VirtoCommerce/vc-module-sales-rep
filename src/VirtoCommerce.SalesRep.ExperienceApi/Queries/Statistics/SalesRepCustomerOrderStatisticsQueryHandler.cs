using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

/// <summary>
/// Order statistics for the current sales rep's customers (VCST-5309). All scoping/currency logic lives in
/// <see cref="SalesRepStatisticsQueryHandlerBase{TQuery,TContext}"/>; this handler only binds the order query/context.
/// </summary>
public class SalesRepCustomerOrderStatisticsQueryHandler
    : SalesRepStatisticsQueryHandlerBase<SalesRepCustomerOrderStatisticsQuery, CustomerOrderStatisticsContext>
{
    public SalesRepCustomerOrderStatisticsQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepCurrencyResolver currencyResolver)
        : base(roleResolver, membershipSearchService, currencyResolver)
    {
    }
}
