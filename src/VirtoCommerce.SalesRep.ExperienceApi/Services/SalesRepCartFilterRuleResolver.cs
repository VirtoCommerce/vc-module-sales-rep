using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepCartFilterRuleResolver : FilterRuleResolverBase<SalesRepCartFilterRule>, ISalesRepCartFilterRuleResolver
{
    public const string ActiveCartsKind = "active-carts";

    public override Task<IList<SalesRepCartFilterRule>> GetRulesAsync(SalesRepFilterRuleContext context)
    {
        IList<SalesRepCartFilterRule> kinds =
        [
            SalesRepCartFilterRule.Create(
                ActiveCartsKind,
                "Active carts",
                names: [ModuleConstants.DefaultCartName]),
        ];

        return Task.FromResult(kinds);
    }

    public virtual async Task<CustomerCartStatisticsCriteria> ApplyStatisticsFilterAsync(string storeId, string filter, CustomerCartStatisticsCriteria criteria)
    {
        if (string.IsNullOrEmpty(filter))
        {
            return criteria;
        }

        var context = SalesRepFilterRuleContext.Create(
            storeId, cultureName: null, criteria.OrganizationIds, criteria.CustomerId, criteria.FromDate, criteria.ToDate);

        var kind = await ResolveNamedRuleAsync(context, filter);

        if (kind == null)
        {
            return null;
        }

        if (!kind.Names.IsNullOrEmpty())
        {
            criteria.Names = kind.Names;
        }

        if (!kind.Types.IsNullOrEmpty())
        {
            criteria.Types = kind.Types;
        }

        if (!kind.ExcludeTypes.IsNullOrEmpty())
        {
            criteria.ExcludeTypes = kind.ExcludeTypes;
        }

        if (!kind.Statuses.IsNullOrEmpty())
        {
            criteria.Statuses = kind.Statuses;
        }

        return criteria;
    }
}
