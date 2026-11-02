using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Xapi.Core.Models;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

internal sealed class PageContextResponse
{
    public StoreResponse Store { get; set; }
    public ApplicationUser User { get; set; }
    public SlugInfoResponse SlugInfo { get; set; }
}
