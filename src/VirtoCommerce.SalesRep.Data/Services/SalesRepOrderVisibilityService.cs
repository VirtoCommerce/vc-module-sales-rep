using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.OrdersModule.Core.Model;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SalesRep.Core.Services;

namespace VirtoCommerce.SalesRep.Data.Services;

// Whether a rep may read an order, decided on the loaded order rather than on whatever the search index said
// about it. An order's OrganizationId is mutable (CustomerOrderEntity.Patch copies it, and the orders REST
// update persists a whole order), so a document indexed before such a change still matches the old
// organization's term filter. Both order surfaces answer through here so they cannot drift apart.
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

    // Resolves the served organizations itself, from the same access service the single-order case uses, so an
    // override of the membership rule reaches both. It asks only about the organizations present on this page,
    // which is one membership query bounded by the page size rather than by the size of the rep's book.
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
