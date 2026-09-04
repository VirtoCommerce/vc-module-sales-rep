using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

// An order's OrganizationId is mutable, so a document indexed before such a change still matches the old
// organization's term filter - visibility is decided on the loaded order, not on the index.
public class SalesRepOrderVisibilityService : ISalesRepOrderVisibilityService
{
    private readonly ISalesRepOrganizationAccessService _organizationAccessService;

    public SalesRepOrderVisibilityService(ISalesRepOrganizationAccessService organizationAccessService)
    {
        _organizationAccessService = organizationAccessService;
    }

    public virtual async Task<bool> IsVisibleAsync(string userId, CustomerOrder order)
    {
        var visible = await FilterVisibleAsync(userId, [order]);

        return visible.Count > 0;
    }

    // Resolves the served organizations for a caller that has not; both paths ask the same access-service
    // primitive, so an override of the membership rule reaches every order surface.
    public virtual async Task<IList<CustomerOrder>> FilterVisibleAsync(string userId, IList<CustomerOrder> orders)
    {
        if (string.IsNullOrEmpty(userId) || orders.IsNullOrEmpty())
        {
            return [];
        }

        var organizationIds = orders
            .Select(x => x?.OrganizationId)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (organizationIds.Count == 0)
        {
            return [];
        }

        var memberships = await _organizationAccessService.GetGrantingMembershipsAsync([userId], organizationIds);

        return FilterVisible(memberships.Select(x => x.OrganizationId).ToList(), orders);
    }

    // For a caller that already holds the served organizations - the set it scoped the search by.
    public virtual IList<CustomerOrder> FilterVisible(IList<string> servedOrganizationIds, IList<CustomerOrder> orders)
    {
        if (servedOrganizationIds.IsNullOrEmpty() || orders.IsNullOrEmpty())
        {
            return [];
        }

        var served = servedOrganizationIds
            .Where(x => !string.IsNullOrEmpty(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return orders
            .Where(x => !string.IsNullOrEmpty(x?.OrganizationId) && served.Contains(x.OrganizationId))
            .ToList();
    }
}
