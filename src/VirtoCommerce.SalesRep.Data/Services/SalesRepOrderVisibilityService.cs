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

    // Resolves the served organizations itself, so an override of the membership rule reaches both surfaces.
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

        var served = memberships
            .Select(x => x.OrganizationId)
            .Where(x => !string.IsNullOrEmpty(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return orders
            .Where(x => !string.IsNullOrEmpty(x?.OrganizationId) && served.Contains(x.OrganizationId))
            .ToList();
    }
}
