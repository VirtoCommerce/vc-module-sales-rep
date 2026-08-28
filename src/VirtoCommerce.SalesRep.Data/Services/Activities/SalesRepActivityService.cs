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
        // which must keep showing their own totals while one of them is selected.
        var plans = _sources
            .SelectMany(source => (source.Categories ?? []).Select(category => (Source: source, Category: category)))
            .ToList();

        if (plans.Count == 0)
        {
            return result;
        }

        // Task.WhenAll keeps the input order, so the results line up with the plans they came from.
        var searches = await Task.WhenAll(plans.Select(async plan =>
        {
            var fetchRows = criteria.Take > 0 && criteria.IsCategoryRequested(plan.Category);
            return (plan.Category, Fetched: fetchRows, Result: await SearchCategoryAsync(criteria, plan, fetchRows));
        }));

        // A fetched category takes its count from its own row fetch, so a tab's count always matches its own list
        // (a separate Take=0 pass could hit a different cache vintage of the analytics source).
        result.CategoryCounts = searches.Select(x => CreateCategoryCount(x.Category, x.Result.TotalCount)).ToList();
        result.Results = GetPage(criteria, [.. searches.Where(x => x.Fetched).Select(x => x.Result)]);
        // The pager is per-tab: only the requested categories add up to the total.
        result.TotalCount = result.CategoryCounts.Where(x => criteria.IsCategoryRequested(x.Category)).Sum(x => x.Count);

        return result;
    }

    // Pagination v1: a requested category returns the top rows of the shared fetch window and the page is sliced
    // from the merge, so deep pages over-fetch proportionally — acceptable for feed-sized reads. The rest are
    // counted with Take=0.
    protected virtual Task<SalesRepActivitySearchResult> SearchCategoryAsync(
        SalesRepActivitySearchCriteria criteria,
        (ISalesRepActivitySource Source, string Category) plan,
        bool fetchRows)
    {
        var sourceCriteria = criteria.CloneTyped();
        sourceCriteria.Categories = [plan.Category];
        sourceCriteria.Take = fetchRows ? GetFetchWindow(criteria) : 0;
        sourceCriteria.Skip = 0;

        return plan.Source.SearchAsync(sourceCriteria);
    }

    // A merged page can only be sliced from the top Skip+Take rows of every category, but asking for exactly that
    // makes every page a different question: Take belongs to a source criteria's cache key, so page 2 re-reads
    // page 1's rows under a new key — another Google round trip per category, per page. Rounding the window up to
    // a fixed bucket makes consecutive pages ask the same question, so only the first page of each bucket reaches
    // the sources. The over-fetch is bounded and close to free upstream: a wider window is the same single report
    // or order search, just with more rows in its response.
    protected virtual int GetFetchWindow(SalesRepActivitySearchCriteria criteria)
    {
        var bucket = ModuleConstants.Activities.PagingWindowBucket;

        return (criteria.Skip + criteria.Take + bucket - 1) / bucket * bucket;
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
