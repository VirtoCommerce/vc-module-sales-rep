using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core;
using VirtoCommerce.SalesRep.Core.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;

namespace VirtoCommerce.SalesRep.ExperienceApi.Services;

public class SalesRepCartFilterRuleResolver : FilterRuleResolverBase<SalesRepCartFilterRule>, ISalesRepCartFilterRuleResolver
{
    public const string ActiveCartsKind = "active-carts";

    public override Task<IList<SalesRepCartFilterRule>> GetRulesAsync(string storeId, string cultureName)
    {
        IList<SalesRepCartFilterRule> kinds =
        [
            // Include-list, not an exclude-list: only the storefront cart ("default") counts. Wishlists,
            // saved-for-later and any cart kind a custom project adds later carry their own list names, so a new
            // type needs no change here — whereas an exclude-list would silently let it into the metrics.
            SalesRepCartFilterRule.Create(
                ActiveCartsKind,
                "Active carts",
                onlyNonEmpty: true,
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

        var kind = await ResolveNamedRuleAsync(storeId, filter);

        if (kind == null)
        {
            return null;
        }

        if (kind.Names is { Count: > 0 })
        {
            criteria.Names = kind.Names;
        }

        if (kind.Types is { Count: > 0 })
        {
            criteria.Types = kind.Types;
        }

        if (kind.ExcludeTypes is { Count: > 0 })
        {
            criteria.ExcludeTypes = kind.ExcludeTypes;
        }

        if (kind.Statuses is { Count: > 0 })
        {
            criteria.Statuses = kind.Statuses;
        }

        criteria.OnlyNonEmpty = kind.OnlyNonEmpty;

        return criteria;
    }
}
