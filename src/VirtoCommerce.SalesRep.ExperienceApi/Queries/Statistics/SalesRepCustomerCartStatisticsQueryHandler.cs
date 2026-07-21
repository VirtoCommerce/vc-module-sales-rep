using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

/// <summary>
/// Cart/project statistics for the current sales rep (dashboard "Active Projects" and related cart widgets). All
/// scoping/currency logic lives in <see cref="SalesRepStatisticsQueryHandlerBase{TQuery,TContext}"/>; this handler
/// only binds the cart query/context.
/// </summary>
public class SalesRepCustomerCartStatisticsQueryHandler
    : SalesRepStatisticsQueryHandlerBase<SalesRepCustomerCartStatisticsQuery, CustomerCartStatisticsContext>
{
    public SalesRepCustomerCartStatisticsQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepCurrencyResolver currencyResolver)
        : base(roleResolver, membershipSearchService, currencyResolver)
    {
    }
}
