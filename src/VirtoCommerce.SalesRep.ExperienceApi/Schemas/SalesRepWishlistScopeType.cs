using VirtoCommerce.SalesRep.Core;
using VirtoCommerce.XCart.Core.Schemas;

namespace VirtoCommerce.SalesRep.ExperienceApi.Schemas;

// Adds the "Customer" wishlist-sharing scope (VCST-5332) to the output enum. EnumerationGraphType throws when
// serializing a value it doesn't know, so the base WishlistScopeType is extended and registered via
// services.OverrideGraphType. Name is pinned so the schema keeps the original "WishlistScopeType" type name.
public class SalesRepWishlistScopeType : WishlistScopeType
{
    public SalesRepWishlistScopeType()
    {
        Name = nameof(WishlistScopeType);

        Add(
            ModuleConstants.Sharing.CustomerScope,
            value: ModuleConstants.Sharing.CustomerScope,
            description: "Customer scope (shared by a Sales Rep with specific customer organizations)");
    }
}
