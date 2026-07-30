using System.Collections.Generic;
using System.Linq;
using VirtoCommerce.CustomerModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Filters;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepOrderFilterRulesQueryHandler : SalesRepFilterRulesQueryHandlerBase<SalesRepOrderFilterRulesQuery, SalesRepOrderFilterRule>
{
    private readonly ISalesRepOrderFilterRuleResolver _filterRuleResolver;

    public SalesRepOrderFilterRulesQueryHandler(
        ISalesRepRoleResolver roleResolver,
        IOrganizationMembershipSearchService membershipSearchService,
        ISalesRepOrderFilterRuleResolver filterRuleResolver)
        : base(roleResolver, membershipSearchService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override IFilterRuleResolver<SalesRepOrderFilterRule> FilterRuleResolver => _filterRuleResolver;

    /// <summary>
    /// Narrows the scope to one customer when the caller is viewing that customer's orders (the same
    /// <c>organizationId</c> the list is scoped by), so the offered statuses match that customer's orders and not the
    /// rep's whole book. An organization the caller does not serve narrows to nothing — no rules, like the list.
    /// </summary>
    protected override SalesRepFilterRuleContext BuildContext(SalesRepOrderFilterRulesQuery request, IList<string> organizationIds)
    {
        var scopedOrganizationIds = string.IsNullOrEmpty(request.OrganizationId)
            ? organizationIds
            : organizationIds.Where(x => x.EqualsIgnoreCase(request.OrganizationId)).ToList();

        return SalesRepFilterRuleContext.Create(request.StoreId, request.CultureName, scopedOrganizationIds, request.UserId);
    }
}
