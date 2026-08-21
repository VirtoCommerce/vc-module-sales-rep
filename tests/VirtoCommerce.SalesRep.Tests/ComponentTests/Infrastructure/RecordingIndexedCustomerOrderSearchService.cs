using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.OrdersModule.Core.Search.Indexed;
using VirtoCommerce.OrdersModule.Core.Services;
using VirtoCommerce.OrdersModule.Data.Search.Indexed;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.SalesRep.Tests.ComponentTests.Infrastructure;

/// <summary>
/// The REAL indexed order search, recording the response group each search asked for. It is the last point that
/// still sees the criteria a GraphQL selection produced, so a test can assert the selection → response-group
/// mapping over the real schema (including the connection wrapper the raw field paths carry).
/// </summary>
internal sealed class RecordingIndexedCustomerOrderSearchService : IndexedCustomerOrderSearchService
{
    public RecordingIndexedCustomerOrderSearchService(
        ISearchRequestBuilderRegistrar searchRequestBuilderRegistrar,
        ISearchProvider searchProvider,
        ICustomerOrderService customerOrderService,
        IConfiguration configuration,
        ILocalizableSettingService localizableSettingService)
        : base(searchRequestBuilderRegistrar, searchProvider, customerOrderService, configuration, localizableSettingService)
    {
    }

    public IList<string> ResponseGroups { get; } = [];

    public override Task<CustomerOrderIndexedSearchResult> SearchCustomerOrdersAsync(CustomerOrderIndexedSearchCriteria criteria)
    {
        ResponseGroups.Add(criteria.ResponseGroup);

        return base.SearchCustomerOrdersAsync(criteria);
    }
}
