using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries.Statistics;

public abstract class SalesRepStatisticsQueryHandlerBase<TQuery, TContext> : SalesRepQueryHandlerBase, IQueryHandler<TQuery, TContext>
    where TQuery : Query<TContext>, ISalesRepStatisticsQuery
    where TContext : SalesRepMonetaryStatisticsContext
{
    private readonly ISalesRepCurrencyResolver _currencyResolver;

    protected SalesRepStatisticsQueryHandlerBase(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCurrencyResolver currencyResolver)
        : base(organizationAccessService)
    {
        _currencyResolver = currencyResolver;
    }

    public virtual async Task<TContext> Handle(TQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(request.UserId))
        {
            return null;
        }

        var organizationIds = await OrganizationAccessService.GetVisibleOrganizationIdsAsync(request.UserId, request.OrganizationId);
        if (organizationIds.Count == 0)
        {
            return null;
        }

        var result = AbstractTypeFactory<TContext>.TryCreateInstance();
        result.OrganizationIds = organizationIds;
        result.SalesRepUserId = request.UserId;
        result.StoreId = request.StoreId;
        result.CurrencyCode = await _currencyResolver.ResolveCurrencyCodeAsync(request.CurrencyCode, request.StoreId);
        return result;
    }
}
