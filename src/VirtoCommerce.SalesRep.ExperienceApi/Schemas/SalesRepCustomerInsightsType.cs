using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using GraphQL.Types;
using GraphQLParser.AST;
using Microsoft.Extensions.Logging;
using VirtoCommerce.GoogleEcommerceAnalyticsModule.Core.Exceptions;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;
using VirtoCommerce.Xapi.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

public class SalesRepCustomerInsightsType : ExtendableGraphType<SalesRepCustomerInsightsContext>
{
    private const string SearchTermsField = "searchTerms";
    private const string BrowsedProductsField = "browsedProducts";
    private const string TakeArgument = "take";
    private const string SortArgument = "sort";

    private readonly ISalesRepCustomerInsightsService _insightsService;
    private readonly ISalesRepProductResolver _productResolver;
    private readonly ILogger<SalesRepCustomerInsightsType> _logger;

    public SalesRepCustomerInsightsType(
        ISalesRepCustomerInsightsService insightsService,
        ISalesRepProductResolver productResolver,
        ILogger<SalesRepCustomerInsightsType> logger)
    {
        _insightsService = insightsService;
        _productResolver = productResolver;
        _logger = logger;

        Name = "SalesRepCustomerInsights";

        Field<NonNullGraphType<BooleanGraphType>>("isAnalyticsAvailable")
            .Description("Whether the collections beside this are measurements: false means they are empty "
                + "for want of a source, not for want of activity. Always reflects configuration, and a selected "
                + "collection whose read fails turns it false as well. Asked on its own, with no collection "
                + "selected, it reports configuration only — no read is made merely to test one.")
            .ResolveAsync(GetIsAnalyticsAvailableAsync);

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepSearchTermType>>>>(SearchTermsField)
            .Description("Search phrases the customer's users looked for. Counts only the 'search' event ('view_search_results' describes the same action and would double-count it). Sort 'date' aggregates a bounded page of the newest 200 hour buckets — older activity is not counted.")
            .Argument<IntGraphType>(TakeArgument, "Rows to return: default 5, clamped to 1..20.")
            .Argument<StringGraphType>(SortArgument, "'count' (top, default) or 'date' (most recent first).")
            .ResolveAsync(async context =>
            {
                var (sortBy, take) = ReadArguments(context.Arguments);
                return await GetSearchTermsAsync(context.Source, sortBy, take);
            });

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepBrowsedProductType>>>>(BrowsedProductsField)
            .Description("Products the customer's users viewed, resolved from the tracked product codes. Sort 'date' aggregates a bounded page of the newest 200 hour buckets — older activity is not counted.")
            .Argument<IntGraphType>(TakeArgument, "Rows to return: default 5, clamped to 1..20.")
            .Argument<StringGraphType>(SortArgument, "'count' (top, default) or 'date' (most recent first).")
            .ResolveAsync(async context =>
            {
                var (sortBy, take) = ReadArguments(context.Arguments);
                return await GetBrowsedProductsAsync(context.Source, sortBy, take);
            });
    }

    // May resolve before the collections, so it awaits their memoized reads. Only the SELECTED collections: a read is
    // never issued merely to answer this field.
    protected virtual async Task<object> GetIsAnalyticsAvailableAsync(IResolveFieldContext<SalesRepCustomerInsightsContext> context)
    {
        foreach (var (field, fieldType) in GetSelectedCollections(context))
        {
            var (sortBy, take) = ReadSiblingArguments(context, field, fieldType);

            if (fieldType.Name == SearchTermsField)
            {
                await GetSearchTermsAsync(context.Source, sortBy, take);
            }
            else
            {
                await GetBrowsedProductsAsync(context.Source, sortBy, take);
            }
        }

        return context.Source.IsAnalyticsAvailable;
    }

    private static IEnumerable<(GraphQLField Field, FieldType FieldType)> GetSelectedCollections(IResolveFieldContext context)
    {
        return context.Parent?.SubFields?.Values
            .Where(x => x.FieldType.Name is SearchTermsField or BrowsedProductsField)
            ?? [];
    }

    private static (string SortBy, int Take) ReadSiblingArguments(IResolveFieldContext context, GraphQLField field, FieldType fieldType)
    {
        return ReadArguments(ExecutionHelper.GetArguments(fieldType.Arguments, field.Arguments, context.Variables, context.Document, field, null));
    }

    // Own and sibling arguments are read alike, so both name the same memoized read.
    private static (string SortBy, int Take) ReadArguments(IDictionary<string, ArgumentValue> arguments)
    {
        var sort = arguments?.GetValueSafe(SortArgument).Value as string;
        var take = arguments?.GetValueSafe(TakeArgument).Value as int?;

        var sortBy = ModuleConstants.Insights.Sort.Date.EqualsIgnoreCase(sort)
            ? ModuleConstants.Insights.Sort.Date
            : ModuleConstants.Insights.Sort.Count;

        return (sortBy, Math.Clamp(take ?? ModuleConstants.Insights.DefaultTake, ModuleConstants.Insights.MinTake, ModuleConstants.Insights.MaxTake));
    }

    protected virtual Task<IList<SalesRepSearchTerm>> GetSearchTermsAsync(SalesRepCustomerInsightsContext insights, string sortBy, int take)
    {
        return insights.GetOrAddSliceAsync($"{SearchTermsField}:{sortBy}:{take}",
            () => ReadAsync(insights, () => _insightsService.GetSearchTermsAsync(CreateCriteria(insights, sortBy, take))));
    }

    protected virtual Task<IList<SalesRepBrowsedProduct>> GetBrowsedProductsAsync(SalesRepCustomerInsightsContext insights, string sortBy, int take)
    {
        return insights.GetOrAddSliceAsync($"{BrowsedProductsField}:{sortBy}:{take}",
            () => ReadAsync(insights, async () =>
            {
                var products = await _insightsService.GetBrowsedProductsAsync(CreateCriteria(insights, sortBy, take));
                await ResolveProductsAsync(insights, products);
                return products;
            }));
    }

    // Both collections come through here, so one catch covers the query.
    protected virtual async Task<IList<T>> ReadAsync<T>(SalesRepCustomerInsightsContext insights, Func<Task<IList<T>>> read)
    {
        try
        {
            return await read();
        }
        catch (AnalyticsException ex)
        {
            _logger.LogWarning(ex, "Customer insights are unavailable for store {StoreId}", insights.StoreId);

            insights.IsAnalyticsAvailable = false;

            return [];
        }
    }

    protected virtual SalesRepCustomerInsightsCriteria CreateCriteria(SalesRepCustomerInsightsContext insights, string sortBy, int take)
    {
        var criteria = AbstractTypeFactory<SalesRepCustomerInsightsCriteria>.TryCreateInstance();

        criteria.OrganizationIds = insights.OrganizationIds;
        criteria.StoreId = insights.StoreId;
        criteria.From = insights.From;
        criteria.To = insights.To;
        criteria.SortBy = sortBy;
        criteria.Take = take;

        return criteria;
    }

    protected virtual Task ResolveProductsAsync(SalesRepCustomerInsightsContext insights, IList<SalesRepBrowsedProduct> products)
    {
        return _productResolver.ResolveAsync(products, insights.StoreId, x => x.Code, (row, product) =>
        {
            row.ProductId = product.ProductId;
            row.ImageUrl = product.ImageUrl;
            row.Name = _productResolver.GetProductName(product, row.Name);
        });
    }
}
