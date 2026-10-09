using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
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

        // Fail closed: without a resolved rep scope no source runs at all, so a contributed source that forgets
        // its own scope guard still cannot return another organization's rows.
        if (criteria.OrganizationIds.IsNullOrEmpty())
        {
            return result;
        }

        // Every registered category is planned so the tabs keep their totals while one is selected; a caller without
        // badges plans only what it asked for, and a DB-backed tab then costs no analytics read.
        var plans = _sources
            .SelectMany(source => (source.Categories ?? []).Select(category => (Source: source, Category: category)))
            .Where(x => criteria.IncludeCategoryCounts || criteria.IsCategoryRequested(x.Category))
            .ToList();

        if (plans.Count == 0)
        {
            return result;
        }

        // A single fetched category has nothing to merge with, so it pages natively; only a merged view needs the
        // fetch window below.
        var pagesNatively = plans.Count(x => IsFetched(criteria, x.Category)) == 1;

        // Task.WhenAll keeps the input order, so the results line up with the plans they came from.
        var searches = await Task.WhenAll(plans.Select(async plan =>
        {
            var fetchRows = IsFetched(criteria, plan.Category);
            return (plan.Category, Fetched: fetchRows, Result: await SearchCategoryAsync(criteria, plan, fetchRows, pagesNatively));
        }));

        // A fetched category counts from its own row fetch, not a separate Take=0 pass that could hit another cache
        // vintage. Grouped, not one row per plan: a category two sources claim would otherwise appear twice.
        result.CategoryCounts = searches
            .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
            .Select(x => CreateCategoryCount(x.Key, x.Sum(plan => plan.Result.TotalCount)))
            .ToList();
        // One unavailable source makes the whole feed's flag false: the counts behind it are then incomplete.
        result.IsAnalyticsAvailable = searches.All(x => x.Result.IsAnalyticsAvailable);
        result.Results = GetPage(criteria, [.. searches.Where(x => x.Fetched).Select(x => x.Result)], pagesNatively ? 0 : criteria.Skip);
        // The pager is per-tab: only the requested categories add up to the total.
        result.TotalCount = result.CategoryCounts.Where(x => criteria.IsCategoryRequested(x.Category)).Sum(x => x.Count);

        return result;
    }

    // A category the caller did not ask for is still counted, with Take=0.
    protected static bool IsFetched(SalesRepActivitySearchCriteria criteria, string category)
    {
        return criteria.Take > 0 && criteria.IsCategoryRequested(category);
    }

    protected virtual Task<SalesRepActivitySearchResult> SearchCategoryAsync(
        SalesRepActivitySearchCriteria criteria,
        (ISalesRepActivitySource Source, string Category) plan,
        bool fetchRows,
        bool pagesNatively)
    {
        var sourceCriteria = criteria.CloneTyped();
        sourceCriteria.Categories = [plan.Category];
        sourceCriteria.Skip = fetchRows && pagesNatively ? criteria.Skip : 0;
        sourceCriteria.Take = fetchRows ? GetFetchTake(criteria, pagesNatively) : 0;

        return plan.Source.SearchAsync(sourceCriteria);
    }

    // A merged page slices the top Skip+Take rows of every category. Take is part of a source's cache key, so deeper
    // pages round it up to a bucket and ask the same question; the first page does not — it is the commonest
    // request, and rounding it would make the cheapest read the dearest.
    protected virtual int GetFetchTake(SalesRepActivitySearchCriteria criteria, bool pagesNatively)
    {
        if (pagesNatively || criteria.Skip == 0)
        {
            return criteria.Take;
        }

        var bucket = ModuleConstants.Activities.PagingWindowBucket;

        return (criteria.Skip + criteria.Take + bucket - 1) / bucket * bucket;
    }

    protected virtual IList<SalesRepActivityEvent> GetPage(
        SalesRepActivitySearchCriteria criteria,
        IList<SalesRepActivitySearchResult> fetchResults,
        int skip)
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
            .Skip(skip)
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
