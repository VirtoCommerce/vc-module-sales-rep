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

// The last point that still sees the criteria a GraphQL selection produced.
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
