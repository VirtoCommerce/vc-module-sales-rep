using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

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
