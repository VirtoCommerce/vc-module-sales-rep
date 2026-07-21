namespace VirtoCommerce.SalesRep.ExperienceApi.Models;

/// <summary>
/// Backing object for the order <c>statistics</c> field: the organizations/store/currency shared by every
/// <c>period</c>/<c>comparison</c> sub-field (see <see cref="SalesRepStatisticsContext"/>). Date ranges come from
/// the sub-field arguments, not here.
/// </summary>
public class CustomerOrderStatisticsContext : SalesRepMonetaryStatisticsContext
{
}
