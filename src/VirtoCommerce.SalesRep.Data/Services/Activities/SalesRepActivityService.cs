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

        // Every registered category is planned, filter or no filter: the counts feed the storefront's category tabs,
        // which must keep showing their own totals while one of them is selected. A caller that does not render
        // those badges plans only what it asked for, and a DB-backed category then costs no analytics read at all.
        var plans = _sources
            .SelectMany(source => (source.Categories ?? []).Select(category => (Source: source, Category: category)))
            .Where(x => criteria.IncludeCategoryCounts || criteria.IsCategoryRequested(x.Category))
            .ToList();

        if (plans.Count == 0)
        {
            return result;
        }

        // A single fetched category has nothing to merge with, so it pages natively: Skip goes to the source and
        // the page comes back ready. Only a merged view needs the fetch window below.
        var pagesNatively = plans.Count(x => IsFetched(criteria, x.Category)) == 1;

        // Task.WhenAll keeps the input order, so the results line up with the plans they came from.
        var searches = await Task.WhenAll(plans.Select(async plan =>
        {
            var fetchRows = IsFetched(criteria, plan.Category);
            return (plan.Category, Fetched: fetchRows, Result: await SearchCategoryAsync(criteria, plan, fetchRows, pagesNatively));
        }));

        // A fetched category takes its count from its own row fetch, so a tab's count always matches its own list
        // (a separate Take=0 pass could hit a different cache vintage of the analytics source).
        result.CategoryCounts = searches.Select(x => CreateCategoryCount(x.Category, x.Result.TotalCount)).ToList();
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

    // A merged page can only be sliced from the top Skip+Take rows of EVERY category it covers, so the merged view
    // pays for depth where a single category pages natively.
    //
    // Asking for exactly Skip+Take would make every page a different question: Take belongs to a source criteria's
    // cache key, so page 2 re-reads page 1's rows under a new key — another Google round trip per category, per
    // page. Deeper pages therefore round up to a fixed bucket and ask the same question. The first page does not:
    // it is by far the most common request (every dashboard widget is one), and rounding it up would make the
    // cheapest read the most expensive one.
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
