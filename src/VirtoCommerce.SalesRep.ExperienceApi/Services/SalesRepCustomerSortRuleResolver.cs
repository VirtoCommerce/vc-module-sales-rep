using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Sorts;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepCustomerSortRuleResolver : SortRuleResolverBase<SalesRepCustomerSortRule>, ISalesRepCustomerSortRuleResolver
{
    public const string MyLastOrdersRuleName = "my-last-orders";

    public const string YtdPurchasesRuleName = "ytd-purchases";

    public const string NameRuleName = "name";

    public override Task<IList<SalesRepCustomerSortRule>> GetRulesAsync(string storeId, string cultureName)
        => Task.FromResult<IList<SalesRepCustomerSortRule>>(
        [
            SalesRepCustomerSortRule.Create(MyLastOrdersRuleName, "My last orders", SortDirection.Descending, supportsDirection: true),
            SalesRepCustomerSortRule.Create(YtdPurchasesRuleName, "YTD purchases", SortDirection.Descending, supportsDirection: true),
            SalesRepCustomerSortRule.Create(NameRuleName, "Customer name", SortDirection.Ascending, supportsDirection: true),
        ]);

    public virtual async Task<SalesRepCustomerSortSpec> ResolveSortAsync(string storeId, string sort)
    {
        var (rule, direction) = await ResolveSortRuleAsync(storeId, sort);

        var spec = BuildSpec(rule?.Name);
        spec.Direction = direction;
        return spec;
    }

    protected virtual SalesRepCustomerSortSpec BuildSpec(string ruleName)
    {
        if (string.Equals(ruleName, NameRuleName, StringComparison.OrdinalIgnoreCase))
        {
            var nameSpec = AbstractTypeFactory<SalesRepCustomerSortSpec>.TryCreateInstance();
            nameSpec.IsOrderDerived = false;
            nameSpec.MemberSortField = "name";
            return nameSpec;
        }

        if (string.Equals(ruleName, YtdPurchasesRuleName, StringComparison.OrdinalIgnoreCase))
        {
            var startOfYear = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var ytdSpec = AbstractTypeFactory<SalesRepCustomerSortSpec>.TryCreateInstance();
            ytdSpec.IsOrderDerived = true;
            ytdSpec.Metric = SalesRepCustomerSortMetric.Total;
            ytdSpec.FromDate = startOfYear;
            return ytdSpec;
        }

        var defaultSpec = AbstractTypeFactory<SalesRepCustomerSortSpec>.TryCreateInstance();
        defaultSpec.IsOrderDerived = true;
        defaultSpec.Metric = SalesRepCustomerSortMetric.LastOrderDate;
        return defaultSpec;
    }
}
