using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using GraphQL.Types;
using GraphQLParser.AST;
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

    public SalesRepCustomerInsightsType(ISalesRepCustomerInsightsService insightsService, ISalesRepProductResolver productResolver)
    {
        _insightsService = insightsService;
        _productResolver = productResolver;

        Name = "SalesRepCustomerInsights";

        Field<DateTimeGraphType>("dataAsOf")
            .Description("Latest event hour (UTC) observed across the selected collections; null when they carry no data or only 'count'-sorted collections are selected (their aggregate rows carry no dates).")
            .ResolveAsync(GetDataAsOfAsync);

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepSearchTermType>>>>(SearchTermsField)
            .Description("Search phrases the customer's users looked for. Counts only the 'search' event ('view_search_results' describes the same action and would double-count it). Sort 'date' aggregates a bounded page of the newest 200 hour buckets — older activity is not counted.")
            .Argument<IntGraphType>(TakeArgument, "Rows to return: default 5, clamped to 1..20.")
            .Argument<StringGraphType>(SortArgument, "'count' (top, default) or 'date' (most recent first).")
            .ResolveAsync(async context =>
            {
                var (sortBy, take) = ReadOwnArguments(context);
                return await GetSearchTermsAsync(context.Source, sortBy, take);
            });

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<SalesRepBrowsedProductType>>>>(BrowsedProductsField)
            .Description("Products the customer's users viewed, resolved from the tracked product codes. Sort 'date' aggregates a bounded page of the newest 200 hour buckets — older activity is not counted.")
            .Argument<IntGraphType>(TakeArgument, "Rows to return: default 5, clamped to 1..20.")
            .Argument<StringGraphType>(SortArgument, "'count' (top, default) or 'date' (most recent first).")
            .ResolveAsync(async context =>
            {
                var (sortBy, take) = ReadOwnArguments(context);
                return await GetBrowsedProductsAsync(context.Source, sortBy, take);
            });
    }

    // dataAsOf covers exactly the sibling collection selections; the memoized per-(collection, sort, take)
    // fetches on the context make the shared reads free regardless of field order.
    private async Task<object> GetDataAsOfAsync(IResolveFieldContext<SalesRepCustomerInsightsContext> context)
    {
        DateTime? result = null;

        foreach (var (field, fieldType) in GetSelectedCollections(context))
        {
            var (sortBy, take) = ReadSiblingArguments(context, field, fieldType);

            var dates = fieldType.Name == SearchTermsField
                ? (await GetSearchTermsAsync(context.Source, sortBy, take)).Select(x => x.LastSearchedDate)
                : (await GetBrowsedProductsAsync(context.Source, sortBy, take)).Select(x => x.LastViewedDate);

            foreach (var date in dates)
            {
                if (date != null && (result == null || date > result))
                {
                    result = date;
                }
            }
        }

        return result;
    }

    private static IEnumerable<(GraphQLField Field, FieldType FieldType)> GetSelectedCollections(IResolveFieldContext context)
    {
        return context.Parent?.SubFields?.Values
            .Where(x => x.FieldType.Name is SearchTermsField or BrowsedProductsField)
            ?? [];
    }

    private static (string SortBy, int Take) ReadSiblingArguments(IResolveFieldContext context, GraphQLField field, FieldType fieldType)
    {
        var arguments = ExecutionHelper.GetArguments(fieldType.Arguments, field.Arguments, context.Variables, context.Document, field, null);

        string sort = null;
        int? take = null;

        if (arguments != null)
        {
            if (arguments.TryGetValue(SortArgument, out var sortValue))
            {
                sort = sortValue.Value as string;
            }

            if (arguments.TryGetValue(TakeArgument, out var takeValue) && takeValue.Value is int takeNumber)
            {
                take = takeNumber;
            }
        }

        return NormalizeArguments(sort, take);
    }

    private static (string SortBy, int Take) ReadOwnArguments(IResolveFieldContext context)
    {
        return NormalizeArguments(context.GetArgument<string>(SortArgument), context.GetArgument<int?>(TakeArgument));
    }

    private static (string SortBy, int Take) NormalizeArguments(string sort, int? take)
    {
        var sortBy = ModuleConstants.Insights.Sort.Date.EqualsIgnoreCase(sort)
            ? ModuleConstants.Insights.Sort.Date
            : ModuleConstants.Insights.Sort.Count;

        return (sortBy, Math.Clamp(take ?? ModuleConstants.Insights.DefaultTake, ModuleConstants.Insights.MinTake, ModuleConstants.Insights.MaxTake));
    }

    private Task<IList<SalesRepSearchTerm>> GetSearchTermsAsync(SalesRepCustomerInsightsContext insights, string sortBy, int take)
    {
        return insights.GetOrAddSliceAsync($"{SearchTermsField}:{sortBy}:{take}",
            () => _insightsService.GetSearchTermsAsync(CreateCriteria(insights, sortBy, take)));
    }

    private Task<IList<SalesRepBrowsedProduct>> GetBrowsedProductsAsync(SalesRepCustomerInsightsContext insights, string sortBy, int take)
    {
        return insights.GetOrAddSliceAsync($"{BrowsedProductsField}:{sortBy}:{take}", async () =>
        {
            var products = await _insightsService.GetBrowsedProductsAsync(CreateCriteria(insights, sortBy, take));
            await ResolveProductsAsync(insights, products);
            return products;
        });
    }

    private static SalesRepCustomerInsightsCriteria CreateCriteria(SalesRepCustomerInsightsContext insights, string sortBy, int take)
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

    private Task ResolveProductsAsync(SalesRepCustomerInsightsContext insights, IList<SalesRepBrowsedProduct> products)
    {
        return _productResolver.ResolveAsync(products, insights.StoreId, x => x.Code, (row, product) =>
        {
            row.ProductId = product.ProductId;
            row.ImageUrl = product.ImageUrl;
            row.Name = product.Name.EmptyToNull() ?? row.Name;
        });
    }
}
