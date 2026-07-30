using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
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
    protected SalesRepFilterRulesQueryHandlerBase(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService)
        : base(roleResolver, membershipSearchService)
    {
    }

    protected abstract IFilterRuleResolver<TRule> FilterRuleResolver { get; }

    protected override Task<IList<TRule>> GetRulesAsync(TQuery request, IList<string> organizationIds)
        => FilterRuleResolver.GetRulesAsync(BuildContext(request, organizationIds));

    protected virtual SalesRepFilterRuleContext BuildContext(TQuery request, IList<string> organizationIds)
    {
        // Only the domains whose vocabulary is derived from records in a window carry a period (see
        // ISalesRepPeriodScopedRulesQuery); for the others the scope is date-unbounded.
        var period = (request as ISalesRepPeriodScopedRulesQuery)?.Period;

        return SalesRepFilterRuleContext.Create(
            request.StoreId, request.CultureName, ScopeOrganizationIds(request, organizationIds), request.UserId, period?.From, period?.To);
    }

    /// <summary>
    /// Narrows the served organizations to the one customer being viewed (see <see cref="ISalesRepCustomerScopedRulesQuery"/>),
    /// so the vocabulary matches that customer's list. An organization the caller does not serve narrows to nothing —
    /// no rules, exactly as the list returns no records. Kept separate from <see cref="BuildContext"/> so a domain
    /// override can't accidentally drop the rest of the scope.
    /// </summary>
    protected virtual IList<string> ScopeOrganizationIds(TQuery request, IList<string> organizationIds)
    {
        var organizationId = (request as ISalesRepCustomerScopedRulesQuery)?.OrganizationId;

        return string.IsNullOrEmpty(organizationId)
            ? organizationIds
            : organizationIds.Where(x => x.EqualsIgnoreCase(organizationId)).ToList();
    }
}
