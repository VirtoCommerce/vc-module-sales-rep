using System.Collections.Generic;
using System.Threading.Tasks;
using VirtoCommerce.SalesRep.Core.Services;
using VirtoCommerce.SalesRep.ExperienceApi.Models;
using VirtoCommerce.SalesRep.ExperienceApi.Services;

namespace VirtoCommerce.SalesRep.ExperienceApi.Queries;

public class SalesRepCustomerFilterRulesQueryHandler : SalesRepRulesQueryHandlerBase<SalesRepCustomerFilterRulesQuery, SalesRepCustomerFilterRule>
{
    private readonly ISalesRepCustomerFilterRuleResolver _filterRuleResolver;

    public SalesRepCustomerFilterRulesQueryHandler(
        ISalesRepOrganizationAccessService organizationAccessService,
        ISalesRepCustomerFilterRuleResolver filterRuleResolver)
        : base(organizationAccessService)
    {
        _filterRuleResolver = filterRuleResolver;
    }

    protected override Task<IList<SalesRepCustomerFilterRule>> GetRulesAsync(SalesRepCustomerFilterRulesQuery request)
        => _filterRuleResolver.GetRulesAsync(request.StoreId, request.CultureName);
}
