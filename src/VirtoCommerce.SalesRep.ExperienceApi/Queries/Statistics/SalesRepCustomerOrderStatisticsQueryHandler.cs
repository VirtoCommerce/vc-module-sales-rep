using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

public class SalesRepCustomerOrderStatisticsQueryHandler
    : SalesRepStatisticsQueryHandlerBase<SalesRepCustomerOrderStatisticsQuery, CustomerOrderStatisticsContext>
{
    public SalesRepCustomerOrderStatisticsQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCurrencyResolver currencyResolver)
        : base(organizationAccessService, currencyResolver)
    {
    }
}
