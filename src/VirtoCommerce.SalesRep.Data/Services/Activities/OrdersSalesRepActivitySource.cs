using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.OrdersModule.Core.Model.Search;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class OrdersSalesRepActivitySource : ISalesRepActivitySource
{
    private static readonly string _responseGroup = (CustomerOrderResponseGroup.Default | CustomerOrderResponseGroup.WithPrices).ToString();

    private readonly ISalesRepCustomerOrderSearchService _customerOrderSearchService;

    public OrdersSalesRepActivitySource(ISalesRepCustomerOrderSearchService customerOrderSearchService)
    {
        _customerOrderSearchService = customerOrderSearchService;
    }

    public IList<string> Categories { get; } = [ModuleConstants.Activities.Categories.Orders];

    public virtual async Task<SalesRepActivitySearchResult> SearchAsync(SalesRepActivitySearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var result = AbstractTypeFactory<SalesRepActivitySearchResult>.TryCreateInstance();

        if (!Categories.Any(criteria.IsCategoryRequested) || criteria.OrganizationIds.IsNullOrEmpty())
        {
            return result;
        }

        var searchCriteria = AbstractTypeFactory<CustomerOrderSearchCriteria>.TryCreateInstance();
        searchCriteria.OrganizationIds = criteria.OrganizationIds.ToArray();
        searchCriteria.CustomerId = criteria.SalesRepUserId;
        searchCriteria.StoreIds = string.IsNullOrEmpty(criteria.StoreId) ? null : [criteria.StoreId];
        searchCriteria.StartDate = criteria.From;
        searchCriteria.EndDate = criteria.To;
        searchCriteria.Skip = criteria.Skip;
        searchCriteria.Take = criteria.Take;
        searchCriteria.ResponseGroup = _responseGroup;

        var searchResult = await _customerOrderSearchService.SearchAsync(searchCriteria);

        result.TotalCount = searchResult.TotalCount;
        result.Results = searchResult.Results.Select(ToEvent).ToList();

        return result;
    }

    protected virtual SalesRepActivityEvent ToEvent(CustomerOrder order)
    {
        var result = AbstractTypeFactory<SalesRepActivityEvent>.TryCreateInstance();

        result.Category = ModuleConstants.Activities.Categories.Orders;
        result.Type = ModuleConstants.Activities.Types.OrderPlaced;
        result.OccurredAt = order.CreatedDate;
        result.Precision = ModuleConstants.Activities.Precision.Exact;
        result.OrganizationId = order.OrganizationId;
        result.OrganizationName = order.OrganizationName;
        result.OrderId = order.Id;
        result.OrderNumber = order.Number;
        result.OrderStatus = order.Status;
        result.OrderTotal = order.Total;
        result.OrderCurrency = order.Currency;

        return result;
    }
}
