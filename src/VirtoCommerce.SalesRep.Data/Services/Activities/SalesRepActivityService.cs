using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services.Activities;

public class SalesRepActivityService : ISalesRepActivityService
{
    private readonly IEnumerable<ISalesRepActivitySource> _sources;

    public SalesRepActivityService(IEnumerable<ISalesRepActivitySource> sources)
    {
        _sources = sources;
    }

    public virtual async Task<SalesRepActivitySearchResult> SearchActivitiesAsync(SalesRepActivitySearchCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var result = AbstractTypeFactory<SalesRepActivitySearchResult>.TryCreateInstance();

        // Every registered category is planned, filter or no filter: the counts feed the storefront's category tabs,
        // which must keep showing their own totals while one of them is selected.
        var plans = _sources
            .SelectMany(source => (source.Categories ?? []).Select(category => (Source: source, Category: category)))
            .ToList();

        if (plans.Count == 0)
        {
            return result;
        }

        List<int> fetchIndexes = [];
        List<int> countIndexes = [];

        for (var index = 0; index < plans.Count; index++)
        {
            var fetchesRows = criteria.Take > 0 && criteria.IsCategoryRequested(plans[index].Category);
            (fetchesRows ? fetchIndexes : countIndexes).Add(index);
        }

        var fetchPlans = fetchIndexes.Select(x => plans[x]).ToList();
        var countPlans = countIndexes.Select(x => plans[x]).ToList();

        // A fetched category takes its count from its own row fetch, so a tab's count always matches its own list
        // (a separate Take=0 pass could hit a different cache vintage of the analytics source).
        var fetchTask = FetchCategoriesAsync(criteria, fetchPlans);
        var countTask = CountCategoriesAsync(criteria, countPlans);
        await Task.WhenAll(fetchTask, countTask);

        var fetchResults = await fetchTask;
        var countResults = await countTask;

        var categoryCounts = new SalesRepActivityCategoryCount[plans.Count];

        for (var index = 0; index < fetchIndexes.Count; index++)
        {
            categoryCounts[fetchIndexes[index]] = CreateCategoryCount(fetchPlans[index].Category, fetchResults[index].TotalCount);
        }

        for (var index = 0; index < countIndexes.Count; index++)
        {
            categoryCounts[countIndexes[index]] = CreateCategoryCount(countPlans[index].Category, countResults[index].TotalCount);
        }

        result.CategoryCounts = [.. categoryCounts];
        result.Results = GetPage(criteria, fetchResults);
        // The pager is per-tab: only the requested categories add up to the total.
        result.TotalCount = result.CategoryCounts.Where(x => criteria.IsCategoryRequested(x.Category)).Sum(x => x.Count);

        return result;
    }

    protected virtual async Task<IList<SalesRepActivitySearchResult>> CountCategoriesAsync(
        SalesRepActivitySearchCriteria criteria,
        IList<(ISalesRepActivitySource Source, string Category)> plans)
    {
        var countTasks = plans.Select(plan =>
        {
            var countCriteria = criteria.CloneTyped();
            countCriteria.Categories = [plan.Category];
            countCriteria.Take = 0;
            countCriteria.Skip = 0;

            return plan.Source.SearchAsync(countCriteria);
        });

        return await Task.WhenAll(countTasks);
    }

    // Pagination v1: every category returns its top Skip+Take rows and the page is sliced from the merge,
    // so deep pages over-fetch proportionally — acceptable for feed-sized reads.
    protected virtual async Task<IList<SalesRepActivitySearchResult>> FetchCategoriesAsync(
        SalesRepActivitySearchCriteria criteria,
        IList<(ISalesRepActivitySource Source, string Category)> plans)
    {
        var fetchTasks = plans.Select(plan =>
        {
            var fetchCriteria = criteria.CloneTyped();
            fetchCriteria.Categories = [plan.Category];
            fetchCriteria.Take = criteria.Skip + criteria.Take;
            fetchCriteria.Skip = 0;

            return plan.Source.SearchAsync(fetchCriteria);
        });

        return await Task.WhenAll(fetchTasks);
    }

    protected virtual IList<SalesRepActivityEvent> GetPage(
        SalesRepActivitySearchCriteria criteria,
        IList<SalesRepActivitySearchResult> fetchResults)
    {
        return fetchResults
            .SelectMany(x => x.Results ?? [])
            .OrderByDescending(x => x.OccurredAt)
            .ThenBy(x => x.Category, StringComparer.Ordinal)
            .ThenBy(x => x.Type, StringComparer.Ordinal)
            .ThenBy(x => x.OrganizationId, StringComparer.Ordinal)
            .ThenBy(x => x.OrderId, StringComparer.Ordinal)
            .ThenBy(x => x.ProductCode, StringComparer.Ordinal)
            .ThenBy(x => x.SearchTerm, StringComparer.Ordinal)
            .Skip(criteria.Skip)
            .Take(criteria.Take)
            .ToList();
    }

    protected static SalesRepActivityCategoryCount CreateCategoryCount(string category, int count)
    {
        var result = AbstractTypeFactory<SalesRepActivityCategoryCount>.TryCreateInstance();

        result.Category = category;
        result.Count = count;

        return result;
    }
}
