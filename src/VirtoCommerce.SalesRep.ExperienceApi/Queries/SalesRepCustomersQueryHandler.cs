using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CoreModule.Core.Currency;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Loads a page of the customer organizations the current Sales Rep serves (VCST-5304), with keyword search, a
/// named customer segment (filter rule) and a named ordering (sort rule). "Name" orders in the members search
/// directly; the order-derived orderings ("my last orders", "ytd purchases") can't be expressed there, so those rank
/// the served organizations by the rep's per-organization order aggregate (one grouped query) and page in memory.
/// </summary>
public class SalesRepCustomersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomersQuery, SalesRepCustomerSearchResult>
{
    private readonly IMemberSearchService _memberSearchService;
    private readonly ISalesRepMemberResponseGroupParser _responseGroupParser;
    private readonly ISalesRepCustomerFilterRuleResolver _filterRuleResolver;
    private readonly ISalesRepCustomerSortRuleResolver _sortRuleResolver;
    private readonly ICustomerOrderStatisticsService _statisticsService;
    private readonly ICurrencyService _currencyService;

    public SalesRepCustomersQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        IMemberSearchService memberSearchService,
        ISalesRepMemberResponseGroupParser responseGroupParser,
        ISalesRepCustomerFilterRuleResolver filterRuleResolver,
        ISalesRepCustomerSortRuleResolver sortRuleResolver,
        ICustomerOrderStatisticsService statisticsService,
        ICurrencyService currencyService)
        : base(roleResolver, membershipSearchService)
    {
        _memberSearchService = memberSearchService;
        _responseGroupParser = responseGroupParser;
        _filterRuleResolver = filterRuleResolver;
        _sortRuleResolver = sortRuleResolver;
        _statisticsService = statisticsService;
        _currencyService = currencyService;
    }

    public virtual async Task<SalesRepCustomerSearchResult> Handle(SalesRepCustomersQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SalesRepCustomerSearchResult>.TryCreateInstance();

        if (string.IsNullOrEmpty(request.UserId))
        {
            return result;
        }

        // The organizations this rep serves (bounded by the rep's served-organization count).
        // OnlyUnlocked: a rep locked in an organization does not see it as a customer.
        var organizationIds = await GetServedOrganizationIdsAsync(request.UserId);
        if (organizationIds.Length == 0)
        {
            return result;
        }

        // Filter (keyword by organization name), sort and page the organizations in the database.
        // GetSearchCriteria carries the request's Keyword/Sort/Skip/Take onto the criteria.
        var membersCriteria = request.GetSearchCriteria<MembersSearchCriteria>();
        membersCriteria.ObjectIds = organizationIds;
        membersCriteria.MemberType = nameof(Organization);
        membersCriteria.RootMembersOnly = false;
        // Load only the member data the caller selected — the organization's addresses only when `address` was
        // requested (id/name/iconUrl are scalar columns loaded with Default). Mirrors the order query's field-driven
        // response group so the list never over-fetches.
        membersCriteria.ResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);

        // Apply the selected customer segment via the shared resolver (same rule the counts use). Null means a
        // segment name was given but is unrecognized — return an empty result rather than every served customer.
        var filteredCriteria = await _filterRuleResolver.ApplyListFilterAsync(request.StoreId, request.Filter, membersCriteria);
        if (filteredCriteria == null)
        {
            return result;
        }

        // The currency the inline orderStatistics figures default to (and the currency the order-derived sort folds
        // totals into). Resolved once; carried onto each row for the graph type's inline field.
        var currencyCode = await ResolvePrimaryCurrencyCodeAsync();

        // Interpret the built-in sort argument as a customer sort-rule name (empty/unknown → the default ordering).
        var sortSpec = await _sortRuleResolver.ResolveSortAsync(request.StoreId, request.Sort);

        return sortSpec.IsOrderDerived
            ? await SearchOrderedByOrderMetricAsync(request, filteredCriteria, sortSpec, currencyCode, result)
            : await SearchOrderedByMemberColumnAsync(request, filteredCriteria, sortSpec, currencyCode, result);
    }

    /// <summary>
    /// Member-column ordering (or the default when the rule maps to one): the members search does keyword + sort +
    /// paging in a single query.
    /// </summary>
    private async Task<SalesRepCustomerSearchResult> SearchOrderedByMemberColumnAsync(
        SalesRepCustomersQuery request,
        MembersSearchCriteria criteria,
        SalesRepCustomerSortSpec sortSpec,
        string currencyCode,
        SalesRepCustomerSearchResult result)
    {
        // Overwrite the raw sort (which carried the rule name) with the resolved member-column expression.
        criteria.Sort = sortSpec.MemberSort;

        var membersSearchResult = await _memberSearchService.SearchMembersAsync(criteria);

        result.TotalCount = membersSearchResult.TotalCount;
        result.Results = membersSearchResult.Results
            .Select(x => SalesRepCustomer.FromOrganization(x, request.StoreId, currencyCode))
            .ToList();
        return result;
    }

    /// <summary>
    /// Order-derived ordering ("my last orders", "ytd purchases"): the members search can't sort by order data, so
    /// load the candidate organizations (segment + keyword; bounded by the served set), rank them by the rep's
    /// per-organization order aggregate over the spec's window, and page the ranked list in memory. Organizations
    /// with no orders in the window rank last but still appear.
    /// </summary>
    private async Task<SalesRepCustomerSearchResult> SearchOrderedByOrderMetricAsync(
        SalesRepCustomersQuery request,
        MembersSearchCriteria criteria,
        SalesRepCustomerSortSpec sortSpec,
        string currencyCode,
        SalesRepCustomerSearchResult result)
    {
        var skip = criteria.Skip;
        var take = criteria.Take;

        // Load every candidate organization once (all matching the segment + keyword), then rank and page in memory —
        // the served set is bounded, so this is a single members query plus a single grouped aggregate query.
        criteria.Skip = 0;
        criteria.Take = criteria.ObjectIds?.Count ?? 0;
        criteria.Sort = null;
        var candidatesResult = await _memberSearchService.SearchMembersAsync(criteria);
        var candidates = candidatesResult.Results;
        if (candidates.Count == 0)
        {
            return result;
        }

        var statisticsCriteria = AbstractTypeFactory<CustomerOrderStatisticsCriteria>.TryCreateInstance();
        statisticsCriteria.OrganizationIds = candidates.Select(x => x.Id).ToArray();
        statisticsCriteria.CustomerId = request.UserId;
        statisticsCriteria.StoreId = request.StoreId;
        statisticsCriteria.CurrencyCode = currencyCode;
        statisticsCriteria.FromDate = sortSpec.FromDate;
        statisticsCriteria.ToDate = sortSpec.ToDate;
        var byOrganization = await _statisticsService.GetStatisticsByOrganizationAsync(statisticsCriteria);

        result.TotalCount = candidates.Count;
        result.Results = RankByMetric(candidates, byOrganization, sortSpec)
            .Skip(skip)
            .Take(take)
            .Select(x => SalesRepCustomer.FromOrganization(x, request.StoreId, currencyCode))
            .ToList();
        return result;
    }

    private static IEnumerable<Member> RankByMetric(
        IList<Member> members,
        IDictionary<string, CustomerOrderStatisticsPeriod> byOrganization,
        SalesRepCustomerSortSpec sortSpec)
    {
        // Name breaks ties so the order is deterministic regardless of the members search's own ordering.
        if (sortSpec.Metric == SalesRepCustomerSortMetric.Total)
        {
            decimal Total(Member m) => byOrganization.TryGetValue(m.Id, out var period) ? period.Total : 0m;
            return sortSpec.Descending
                ? members.OrderByDescending(Total).ThenBy(m => m.Name)
                : members.OrderBy(Total).ThenBy(m => m.Name);
        }

        DateTime LastOrder(Member m) => byOrganization.TryGetValue(m.Id, out var period) && period.LastOrderDate.HasValue
            ? period.LastOrderDate.Value
            : DateTime.MinValue;
        return sortSpec.Descending
            ? members.OrderByDescending(LastOrder).ThenBy(m => m.Name)
            : members.OrderBy(LastOrder).ThenBy(m => m.Name);
    }

    private async Task<string> ResolvePrimaryCurrencyCodeAsync()
    {
        var currencies = await _currencyService.GetAllCurrenciesAsync();
        return currencies.FirstOrDefault(x => x.IsPrimary)?.Code;
    }
}
