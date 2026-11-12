using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Model;
using VirtoCommerce.CustomerModule.Core.Model.Search;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.Core.Services.Statistics;
using VirtoCommerce.SalesRep.ExperienceApi.Extensions;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomersQueryHandler : SalesRepQueryHandlerBase, IQueryHandler<SalesRepCustomersQuery, SalesRepCustomerSearchResult>
{
    private readonly IMemberSearchService _memberSearchService;
    private readonly ISalesRepMemberResponseGroupParser _responseGroupParser;
    private readonly ISalesRepCustomerFilterRuleResolver _filterRuleResolver;
    private readonly ISalesRepCustomerSortRuleResolver _sortRuleResolver;
    private readonly ICustomerOrderStatisticsService _statisticsService;
    private readonly ISalesRepCurrencyResolver _currencyResolver;
    private readonly ISalesRepPrimaryContactResolver _primaryContactResolver;

    public SalesRepCustomersQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        IMemberSearchService memberSearchService,
        ISalesRepMemberResponseGroupParser responseGroupParser,
        ISalesRepCustomerFilterRuleResolver filterRuleResolver,
        ISalesRepCustomerSortRuleResolver sortRuleResolver,
        ICustomerOrderStatisticsService statisticsService,
        ISalesRepCurrencyResolver currencyResolver,
        ISalesRepPrimaryContactResolver primaryContactResolver)
        : base(organizationAccessService)
    {
        _memberSearchService = memberSearchService;
        _responseGroupParser = responseGroupParser;
        _filterRuleResolver = filterRuleResolver;
        _sortRuleResolver = sortRuleResolver;
        _statisticsService = statisticsService;
        _currencyResolver = currencyResolver;
        _primaryContactResolver = primaryContactResolver;
    }

    public virtual async Task<SalesRepCustomerSearchResult> Handle(SalesRepCustomersQuery request, CancellationToken cancellationToken)
    {
        var result = AbstractTypeFactory<SalesRepCustomerSearchResult>.TryCreateInstance();

        if (string.IsNullOrEmpty(request.UserId))
        {
            return result;
        }

        var organizationIds = await OrganizationAccessService.GetServedOrganizationIdsAsync(request.UserId);
        if (organizationIds.Count == 0)
        {
            return result;
        }

        var membersCriteria = request.GetSearchCriteria<MembersSearchCriteria>();
        membersCriteria.ObjectIds = organizationIds;
        membersCriteria.MemberType = nameof(Organization);
        membersCriteria.RootMembersOnly = false;
        membersCriteria.ResponseGroup = _responseGroupParser.GetResponseGroup(request.IncludeFields);

        var filteredCriteria = await _filterRuleResolver.ApplyListFilterAsync(request.StoreId, request.Filter, membersCriteria);
        if (filteredCriteria == null)
        {
            return result;
        }

        var currencyCode = await _currencyResolver.ResolveCurrencyCodeAsync(requestedCurrencyCode: null, storeId: request.StoreId);

        var sortSpec = await _sortRuleResolver.ResolveSortAsync(request.StoreId, request.Sort);

        return sortSpec.IsOrderDerived
            ? await SearchOrderedByOrderMetricAsync(request, filteredCriteria, sortSpec, currencyCode, result)
            : await SearchOrderedByMemberColumnAsync(request, filteredCriteria, sortSpec, currencyCode, result);
    }

    private async Task<SalesRepCustomerSearchResult> SearchOrderedByMemberColumnAsync(
        SalesRepCustomersQuery request,
        MembersSearchCriteria criteria,
        SalesRepCustomerSortSpec sortSpec,
        string currencyCode,
        SalesRepCustomerSearchResult result)
    {
        criteria.Sort = $"{sortSpec.MemberSortField}:{sortSpec.Direction.ToToken()}";

        var membersSearchResult = await _memberSearchService.SearchMembersAsync(criteria);

        result.TotalCount = membersSearchResult.TotalCount;
        result.Results = await ToCustomersAsync(membersSearchResult.Results, request, currencyCode);
        return result;
    }

    private async Task<SalesRepCustomerSearchResult> SearchOrderedByOrderMetricAsync(
        SalesRepCustomersQuery request,
        MembersSearchCriteria criteria,
        SalesRepCustomerSortSpec sortSpec,
        string currencyCode,
        SalesRepCustomerSearchResult result)
    {
        var skip = criteria.Skip;
        var take = criteria.Take;

        // Order-derived metrics live in the orders, not the member index, so we can't page in the DB: load every
        // served-org candidate (bounded by the rep's assignment count), then rank + page in memory. Ranking reads
        // only ids and names, so the candidates load with the default group and the page reloads with the selected one.
        var responseGroup = criteria.ResponseGroup;
        criteria.Skip = 0;
        criteria.Take = criteria.ObjectIds?.Count ?? 0;
        criteria.Sort = null;
        criteria.ResponseGroup = nameof(MemberResponseGroup.Default);
        var candidatesResult = await _memberSearchService.SearchMembersAsync(criteria);
        var candidates = candidatesResult.Results;
        if (candidates.Count == 0)
        {
            return result;
        }

        var statisticsCriteria = AbstractTypeFactory<CustomerOrderStatisticsCriteria>.TryCreateInstance();
        statisticsCriteria.OrganizationIds = candidates.Select(x => x.Id).ToList();
        statisticsCriteria.CustomerId = request.UserId;
        statisticsCriteria.StoreId = request.StoreId;
        statisticsCriteria.CurrencyCode = currencyCode;
        statisticsCriteria.FromDate = sortSpec.FromDate;
        statisticsCriteria.ToDate = sortSpec.ToDate;
        var byOrganization = await _statisticsService.GetStatisticsByOrganizationAsync(statisticsCriteria);

        result.TotalCount = candidates.Count;
        var rankedPage = RankByMetric(candidates, byOrganization, sortSpec)
            .Skip(skip)
            .Take(take)
            .ToList();
        var page = await ReloadWithResponseGroupAsync(rankedPage, responseGroup);
        result.Results = await ToCustomersAsync(page, request, currencyCode);
        return result;
    }

    protected virtual async Task<IList<Member>> ReloadWithResponseGroupAsync(IList<Member> organizations, string responseGroup)
    {
        if (organizations.Count == 0 || responseGroup == nameof(MemberResponseGroup.Default))
        {
            return organizations;
        }

        var criteria = AbstractTypeFactory<MembersSearchCriteria>.TryCreateInstance();
        criteria.ObjectIds = organizations.Select(x => x.Id).ToList();
        criteria.MemberType = nameof(Organization);
        criteria.RootMembersOnly = false;
        criteria.ResponseGroup = responseGroup;
        criteria.Take = organizations.Count;
        var reloaded = (await _memberSearchService.SearchMembersAsync(criteria)).Results
            .DistinctBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        return organizations
            .Select(x => reloaded.TryGetValue(x.Id, out var organization) ? organization : x)
            .ToList();
    }

    protected virtual async Task<IList<SalesRepCustomer>> ToCustomersAsync(
        IList<Member> organizations,
        SalesRepCustomersQuery request,
        string currencyCode)
    {
        var primaryContacts = request.IncludeFields.IncludesPrimaryContact()
            ? await _primaryContactResolver.ResolvePrimaryContactsAsync(
                organizations.OfType<Organization>().ToList(),
                request.IncludeFields.GetPrimaryContactResponseGroup())
            : new Dictionary<string, Contact>();

        var customers = new List<SalesRepCustomer>(organizations.Count);
        foreach (var organization in organizations)
        {
            primaryContacts.TryGetValue(organization.Id, out var primaryContact);
            customers.Add(SalesRepCustomer.FromOrganization(organization, request.StoreId, currencyCode, primaryContact));
        }

        return customers;
    }

    private static IEnumerable<Member> RankByMetric(
        IList<Member> members,
        IDictionary<string, CustomerOrderStatisticsPeriod> byOrganization,
        SalesRepCustomerSortSpec sortSpec)
    {
        var descending = sortSpec.Direction == SortDirection.Descending;
        if (sortSpec.Metric == SalesRepCustomerSortMetric.Total)
        {
            decimal Total(Member m) => byOrganization.TryGetValue(m.Id, out var period) ? period.Total : 0m;
            var ranked = descending ? members.OrderByDescending(Total) : members.OrderBy(Total);
            return ranked.ThenBy(m => m.Name);
        }

        DateTime LastOrder(Member m) => byOrganization.TryGetValue(m.Id, out var period) && period.LastOrderDate.HasValue
            ? period.LastOrderDate.Value
            : DateTime.MinValue;
        var rankedByDate = descending ? members.OrderByDescending(LastOrder) : members.OrderBy(LastOrder);
        return rankedByDate.ThenBy(m => m.Name);
    }
}
