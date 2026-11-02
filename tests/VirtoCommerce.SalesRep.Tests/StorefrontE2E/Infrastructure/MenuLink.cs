using System.Collections.Generic;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

internal sealed class MenuLink
{
    public string Title { get; set; }
    public string Url { get; set; }
    public IList<MenuLink> ChildItems { get; set; } = [];
}
