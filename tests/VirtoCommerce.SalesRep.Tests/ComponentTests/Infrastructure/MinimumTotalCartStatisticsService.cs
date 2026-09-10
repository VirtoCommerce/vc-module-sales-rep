using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using VirtoCommerce.CartModule.Data.Model;
using VirtoCommerce.CartModule.Data.Repositories;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.Platform.Core.Caching;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Data.Services.Statistics;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// Stands in for a custom project that narrows the statistics cart set through the module's documented
/// <c>BuildQuery</c> seam. Used to assert the override reaches the line-item quantities as well as the
/// cart-level figures.
/// </summary>
internal sealed class MinimumTotalCartStatisticsService : CustomerCartStatisticsService
{
    public const decimal MinimumTotal = 200m;

    public MinimumTotalCartStatisticsService(
        Func<ICartRepository> cartRepositoryFactory,
        ICurrencyService currencyService,
        IPlatformMemoryCache platformMemoryCache,
        ISettingsManager settingsManager,
        ILogger<CustomerCartStatisticsService> logger)
        : base(cartRepositoryFactory, currencyService, platformMemoryCache, settingsManager, logger)
    {
    }

    protected override IQueryable<ShoppingCartEntity> BuildQuery(ICartRepository repository, CustomerCartStatisticsCriteria criteria)
    {
        return base.BuildQuery(repository, criteria).Where(x => x.Total >= MinimumTotal);
    }
}
