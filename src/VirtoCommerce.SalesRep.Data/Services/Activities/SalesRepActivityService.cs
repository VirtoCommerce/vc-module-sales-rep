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

        var plans = _sources
            .SelectMany(source => criteria.GetEffectiveCategories(source.Categories)
                .Select(category => (Source: source, Category: category)))
            .ToList();

        if (plans.Count == 0)
        {
            return result;
        }

        if (criteria.Take > 0)
        {
            // Counts come from the same per-category fetch as the rows, so a tab's count always matches its own list
            // (a separate Take=0 pass could hit a different cache vintage of the analytics source).
            var fetchResults = await FetchCategoriesAsync(criteria, plans);
            result.CategoryCounts = plans
                .Select((plan, index) => CreateCategoryCount(plan.Category, fetchResults[index].TotalCount))
                .ToList();
            result.Results = GetPage(criteria, fetchResults);
        }
        else
        {
            result.CategoryCounts = await GetCategoryCountsAsync(criteria, plans);
        }

        result.TotalCount = result.CategoryCounts.Sum(x => x.Count);

        return result;
    }

    protected virtual async Task<IList<SalesRepActivityCategoryCount>> GetCategoryCountsAsync(
        SalesRepActivitySearchCriteria criteria,
        IList<(ISalesRepActivitySource Source, string Category)> plans)
    {
        var countTasks = plans.Select(async plan =>
        {
            var countCriteria = criteria.CloneTyped();
            countCriteria.Categories = [plan.Category];
            countCriteria.Take = 0;
            countCriteria.Skip = 0;

            var countResult = await plan.Source.SearchAsync(countCriteria);

            return CreateCategoryCount(plan.Category, countResult.TotalCount);
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
