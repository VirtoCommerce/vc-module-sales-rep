namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Backing object for the cart-statistics field: the organizations/store/currency shared by every
/// <c>period</c>/<c>comparison</c> sub-field (see <see cref="SalesRepStatisticsContext"/>). Date ranges and cart
/// kinds come from the sub-field arguments, not here.
/// </summary>
public class CustomerCartStatisticsContext : SalesRepStatisticsContext
{
}
