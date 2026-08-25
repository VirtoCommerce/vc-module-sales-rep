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
            .Select(source => (Source: source, Categories: criteria.GetEffectiveCategories(source.Categories)))
            .Where(x => x.Categories.Count > 0)
            .ToList();

        if (plans.Count == 0)
        {
            return result;
        }

        result.CategoryCounts = await GetCategoryCountsAsync(criteria, plans);
        result.TotalCount = result.CategoryCounts.Sum(x => x.Count);

        if (criteria.Take > 0)
        {
            result.Results = await GetPageAsync(criteria, plans);
        }

        return result;
    }

    protected virtual async Task<IList<SalesRepActivityCategoryCount>> GetCategoryCountsAsync(
        SalesRepActivitySearchCriteria criteria,
        IList<(ISalesRepActivitySource Source, IList<string> Categories)> plans)
    {
        var countTasks = plans
            .SelectMany(plan => plan.Categories.Select(category => (plan.Source, Category: category)))
            .Select(async x =>
            {
                var countCriteria = criteria.CloneTyped();
                countCriteria.Categories = [x.Category];
                countCriteria.Take = 0;
                countCriteria.Skip = 0;

                var countResult = await x.Source.SearchAsync(countCriteria);

                var categoryCount = AbstractTypeFactory<SalesRepActivityCategoryCount>.TryCreateInstance();
                categoryCount.Category = x.Category;
                categoryCount.Count = countResult.TotalCount;
                return categoryCount;
            });

        return await Task.WhenAll(countTasks);
    }

    // Pagination v1: every source returns its top Skip+Take rows and the page is sliced from the merge,
    // so deep pages over-fetch proportionally — acceptable for feed-sized reads.
    protected virtual async Task<IList<SalesRepActivityEvent>> GetPageAsync(
        SalesRepActivitySearchCriteria criteria,
        IList<(ISalesRepActivitySource Source, IList<string> Categories)> plans)
    {
        var fetchTasks = plans.Select(plan =>
        {
            var fetchCriteria = criteria.CloneTyped();
            fetchCriteria.Categories = plan.Categories;
            fetchCriteria.Take = criteria.Skip + criteria.Take;
            fetchCriteria.Skip = 0;

            return plan.Source.SearchAsync(fetchCriteria);
        });

        var results = await Task.WhenAll(fetchTasks);

        return results
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
}
