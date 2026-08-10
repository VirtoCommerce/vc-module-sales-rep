using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

public class SalesRepCustomerCartStatisticsQueryHandler
    : SalesRepStatisticsQueryHandlerBase<SalesRepCustomerCartStatisticsQuery, CustomerCartStatisticsContext>
{
    public SalesRepCustomerCartStatisticsQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCurrencyResolver currencyResolver)
        : base(organizationAccessService, currencyResolver)
    {
    }
}
