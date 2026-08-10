using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.Xapi.Core.BaseQueries;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

/// <summary>
/// Discovery handler for a filter-rule domain: hands the resolver the caller's scope (store, culture, served
/// organizations and the rep as creator) so a data-derived rule set is built over exactly the records the caller's
/// list will search.
/// </summary>
public abstract class SalesRepFilterRulesQueryHandlerBase<TQuery, TRule> : SalesRepRulesQueryHandlerBase<TQuery, TRule>
    where TQuery : Query<IList<TRule>>, ISalesRepRulesQuery
    where TRule : class, INamedFilterRule
{
    protected SalesRepFilterRulesQueryHandlerBase(ISalesRepOrganizationAccessService organizationAccessService)
        : base(organizationAccessService)
    {
    }

    protected abstract IFilterRuleResolver<TRule> FilterRuleResolver { get; }

    protected override Task<IList<TRule>> GetRulesAsync(TQuery request, IList<string> organizationIds)
        => FilterRuleResolver.GetRulesAsync(BuildContext(request, organizationIds));

    protected virtual SalesRepFilterRuleContext BuildContext(TQuery request, IList<string> organizationIds)
    {
        // Only a vocabulary derived from records in a window carries a period; for the others the scope is unbounded.
        var period = (request as ISalesRepScopedRulesQuery)?.Period;

        return SalesRepFilterRuleContext.Create(
            request.StoreId, request.CultureName, organizationIds, request.UserId, period?.From, period?.To);
    }
}
