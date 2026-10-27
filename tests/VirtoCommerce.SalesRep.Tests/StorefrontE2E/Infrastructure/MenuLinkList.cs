using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

internal sealed class MenuLinkList
{
    public string Name { get; set; }
    public IList<MenuLink> Items { get; set; } = [];
}
